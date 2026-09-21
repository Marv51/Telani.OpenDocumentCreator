using System.Globalization;
using OpenDocumentCreator;
using OpenDocumentCreator.DataTypes;
using OpenDocumentCreator.Styles;

namespace OdcCompat;

/// <summary>
/// Builds a recipe and writes the result to a file.
///
/// This one file is compiled into both comparison projects, against a different version of the
/// library in each. That is what makes the comparison meaningful: the two sides are not merely
/// written to look the same, they are the same source.
///
/// Everything called here has to exist in both versions, which rules out the API added since:
/// SetColumnsDefaultCellStyle and the WriteColumn overload that carries a style have no counterpart
/// in the released build, so they cannot be compared against it and are covered by unit tests
/// instead.
/// </summary>
internal static class Build
{
    public static void WriteAll(string outputDirectory, int firstSeed, int count)
    {
        Directory.CreateDirectory(outputDirectory);

        for (var seed = firstSeed; seed < firstSeed + count; seed++)
        {
            var recipe = Recipe.Generate(seed);
            var path = Path.Combine(outputDirectory, seed.ToString(CultureInfo.InvariantCulture) + ".ods");

            try
            {
                File.WriteAllBytes(path, Save(recipe));
            }
            catch (Exception ex)
            {
                File.WriteAllText(path + ".threw", ex.GetType().FullName + ": " + ex.Message);
            }
        }
    }

    private static byte[] Save(Recipe recipe)
    {
        var doc = new OpenDocumentSpreadsheet("Compat");

        var cellStyles = AddStyles(doc, recipe.CellStyles);
        var columnStyles = AddStyles(doc, recipe.ColumnStyles);
        var rowStyles = AddStyles(doc, recipe.RowStyles);
        AddStyles(doc, recipe.ExtraStyles);

        foreach (var table in recipe.Tables)
        {
            BuildTable(doc, table, cellStyles, columnStyles, rowStyles);
        }

        // 1.0.4 has no leaveOpen and closes the stream, so the bytes are taken from the closed
        // stream. That works on both versions, so the same call serves for each.
        var mem = new MemoryStream();
        doc.Save(mem, false, "Calibri").GetAwaiter().GetResult();
        return mem.ToArray();
    }

    private static void BuildTable(
        OpenDocumentSpreadsheet doc,
        TableRecipe table,
        OpenDocumentStyle[] cellStyles,
        OpenDocumentStyle[] columnStyles,
        OpenDocumentStyle[] rowStyles)
    {
        var ag = new AutoGrid(doc, doc.GetUniqueTableName(table.Name), table.Rows, table.Columns, table.DefaultWidth);
        doc.Tables.Add(ag);

        if (table.ColumnWidths.Count > 0)
        {
            ag.SetColumnsWidth(table.ColumnWidthStart, [.. table.ColumnWidths]);
        }

        if (table.ColumnStyles.Count > 0)
        {
            ag.SetColumnsStyle(table.ColumnStyleStart, [.. table.ColumnStyles.Select(i => columnStyles[i].Name!)]);
        }

        foreach (var step in table.Cells)
        {
            WriteCell(ag, step, cellStyles);
        }

        foreach (var bulk in table.BulkWrites)
        {
            WriteBulk(ag, bulk, cellStyles);
        }

        foreach (var span in table.Spans)
        {
            // Both versions reject some spans for the same reasons; what matters is that they
            // agree, which the file comparison shows.
            Attempt(() => ag.SetCellSpan(span.X, span.Y, span.RowSpan, span.ColumnSpan));
        }

        foreach (var row in table.StyledRows)
        {
            Attempt(() => ag.WriteRowStyle(row, rowStyles[row % rowStyles.Length]));
        }
    }

    private static void WriteCell(AutoGrid ag, CellStep step, OpenDocumentStyle[] cellStyles)
    {
        var style = step.StyleIndex >= 0 && step.StyleIndex < cellStyles.Length ? cellStyles[step.StyleIndex] : null;

        var cell = step.Kind switch
        {
            CellKind.Empty => new OpenDocumentCell((string?)null),
            CellKind.Text => new OpenDocumentCell(step.Text),
            CellKind.MultiLineText => new OpenDocumentCell(step.Text + "\nsecond\n\nfourth"),
            CellKind.TextWithSpaces => new OpenDocumentCell(step.Text + "  two   three   "),
            CellKind.TextNeedingEscapes => new OpenDocumentCell(step.Text + " & < > \" '"),
            CellKind.Number => new OpenDocumentCell(step.Number),
            CellKind.Formula => new OpenDocumentCell(step.Number) { Formula = Formula(step) },
            CellKind.Link => new OpenDocumentCell(Link(step)),
            CellKind.Frame => new OpenDocumentCell { Frame = Frame(step) },
            _ => new OpenDocumentCell(),
        };

        if (step.EmptyLines != EmptyLineMode.Unset)
        {
            cell.EmptyLines = step.EmptyLines switch
            {
                EmptyLineMode.Collapse => EmptyLineHandling.Collapse,
                EmptyLineMode.Preserve => EmptyLineHandling.Preserve,
                _ => EmptyLineHandling.TrimEnds,
            };
        }

        if (step.Repeat > 1)
        {
            cell.NumberColumnsRepeated = step.Repeat;
        }

        if (step.ColumnsSpanned > 1)
        {
            cell.ColumnsSpanned = step.ColumnsSpanned;
        }

        if (step.RowsSpanned > 1)
        {
            cell.RowsSpanned = step.RowsSpanned;
        }

        if (step.IsCovered)
        {
            cell.IsCovered = true;
        }

        Attempt(() => ag.WriteCell(step.X, step.Y, cell, style));
    }

    private static void WriteBulk(AutoGrid ag, BulkStep bulk, OpenDocumentStyle[] cellStyles)
    {
        var style = bulk.StyleIndex >= 0 && bulk.StyleIndex < cellStyles.Length ? cellStyles[bulk.StyleIndex] : null;

        switch (bulk.Kind)
        {
            case BulkKind.WriteColumn:
                Attempt(() => ag.WriteColumn(bulk.X, bulk.Y, bulk.Values.Select(r => r[0])));
                break;

            case BulkKind.WriteColumns:
                Attempt(() => ag.WriteColumns(bulk.X, bulk.Y, bulk.Values.Select(r => r.AsEnumerable())));
                break;

            case BulkKind.WriteRow:
                Attempt(() => ag.WriteRow(bulk.X, bulk.Y, MakeRow(bulk.Values[0], bulk.Build, style)));
                break;

            case BulkKind.WriteRowsArray:
                Attempt(() => ag.WriteRows(bulk.X, bulk.Y, [.. bulk.Values.Select(r => MakeRow(r, bulk.Build, style))]));
                break;

            case BulkKind.WriteRowsEnumerable:
                // Not an array, so the IEnumerable overload is taken rather than the params one:
                // the two reach the grid differently.
                Attempt(() => ag.WriteRows(bulk.X, bulk.Y, bulk.Values.Select(r => MakeRow(r, bulk.Build, style))));
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Fills a row. The released version offers several ways to do it and they do not all end in
    /// the same place, so which one is used is part of the recipe.
    /// </summary>
    /// <param name="values">the cell texts</param>
    /// <param name="build">how to put the row together</param>
    /// <param name="style">the style for the cells, if any</param>
    /// <returns>the row</returns>
    private static Row MakeRow(IReadOnlyList<string> values, RowBuild build, OpenDocumentStyle? style)
    {
        var row = style is null ? new Row() : new Row(style);

        switch (build)
        {
            case RowBuild.AddString:
                foreach (var value in values)
                {
                    row.Add(value);
                }
                break;

            case RowBuild.AddTuple:
                foreach (var value in values)
                {
                    if (style is null)
                    {
                        row.Add(value);
                    }
                    else
                    {
                        row.Add((value, style));
                    }
                }
                break;

            case RowBuild.InsertCell:
                foreach (var value in values)
                {
                    row.InsertCell(value);
                }
                break;

            case RowBuild.InsertCellWithStyle:
                foreach (var value in values)
                {
                    row.InsertCell(value, style!);
                }
                break;

            case RowBuild.InsertCells:
                row.InsertCells([.. values.Select(v => new OpenDocumentCell(v))]);
                break;

            case RowBuild.TemplateString:
                // Cells are separated by a pipe and a lone * is a covered cell. The values go in
                // as the format arguments, so the braces in the template have to line up with
                // them.
                row.InsertCellsFromTemplateString(style, Template(values.Count), [.. values]);
                break;

            case RowBuild.AddThenReplace:
                foreach (var value in values)
                {
                    row.Add(new OpenDocumentCell(value));
                }
                if (row.Count > 0)
                {
                    row.Replace(row.Count - 1, new OpenDocumentCell("replaced  text "));
                }
                break;

            default:
                foreach (var value in values)
                {
                    row.Add(new OpenDocumentCell(value));
                }
                break;
        }

        return row;
    }

    /// <summary>
    /// A template for <c>InsertCellsFromTemplateString</c>: one placeholder per value, with a
    /// covered cell thrown in so that branch is taken too.
    /// </summary>
    /// <param name="count">how many values there are</param>
    /// <returns>the template</returns>
    private static string Template(int count)
        => string.Join("|", Enumerable.Range(0, count).Select(i => i % 4 == 3 ? "*" : "{" + i + "}"));

    private static string Formula(CellStep step)
        => "of:=SUM([.A" + ((step.X % 7) + 1) + ":.A" + ((step.Y % 9) + 2) + "])";

    private static Uri Link(CellStep step)
        => (step.X % 3) switch
        {
            // The second and third carry characters the writer has to escape in an attribute.
            0 => new Uri("https://example.invalid/" + step.X + "/" + step.Y),
            1 => new Uri("https://example.invalid/search?a=1&b=2&c=" + step.X),
            _ => new Uri("https://example.invalid/path%20with%20spaces/" + step.Y + "#frag"),
        };

    private static OpenDocumentFrame Frame(CellStep step)
    {
        var frame = new OpenDocumentFrame { Name = "f" + step.X + "_" + step.Y };

        switch (step.Frame)
        {
            case FrameKind.Positioned:
                frame.RelWidth = "50%";
                frame.RelHeight = "25%";
                frame.ZIndex = step.X;
                break;

            case FrameKind.WithTextBox:
                frame.TextBox = new OpenDocumentTextBox();
                frame.TextBox.Paragraphs.Add(new OpenDocumentParagraph(step.Text));
                frame.TextBox.Paragraphs.Add(new OpenDocumentParagraph("second  paragraph & more"));
                break;

            case FrameKind.WithImage:
                // Href is the only part of an image the released version lets us set.
                frame.Image = new OpenDocumentImage
                {
                    Href = "Pictures/" + step.X + "_" + step.Y + ".png",
                };
                break;

            default:
                break;
        }

        return frame;
    }


    /// <summary>
    /// Registers a run of styles on the document and returns them in recipe order.
    /// </summary>
    /// <param name="doc">the document</param>
    /// <param name="recipes">the styles to add</param>
    /// <returns>the styles</returns>
    private static OpenDocumentStyle[] AddStyles(OpenDocumentSpreadsheet doc, IReadOnlyList<StyleRecipe> recipes)
    {
        var styles = new OpenDocumentStyle[recipes.Count];

        for (var i = 0; i < recipes.Count; i++)
        {
            styles[i] = MakeStyle(recipes[i]);
            doc.Styles.Add(styles[i].Name!, styles[i]);
        }

        return styles;
    }

    private static OpenDocumentStyle MakeStyle(StyleRecipe recipe)
    {
        var style = new OpenDocumentStyle
        {
            Name = recipe.Name,
            Family = recipe.Family switch
            {
                StyleTarget.TableCell => StyleFamily.TableCell,
                StyleTarget.TableColumn => StyleFamily.TableColumn,
                StyleTarget.TableRow => StyleFamily.TableRow,
                StyleTarget.Table => StyleFamily.Table,
                StyleTarget.Paragraph => StyleFamily.Paragraph,
                _ => StyleFamily.Graphic,
            },
            ParentStyleName = recipe.ParentName,
            DataStyleName = recipe.DataStyleName,
        };

        if (recipe.Cell is { } cell)
        {
            style.TableCellProperties = new TableCellProperties
            {
                Border = Line(cell.Border),
                BorderLeft = Line(cell.BorderLeft),
                BorderTop = Line(cell.BorderTop),
                DiagonalTopLeftBottomRight = Line(cell.Diagonal),
                BackgroundColor = Colour(cell.BackgroundColor),
                VerticalAlign = Pick<VerticalAlign>(cell.VerticalAlign),
                WrapOption = Pick<WrapOption>(cell.WrapOption),
                TextAlignSource = Pick<TextAlignSource>(cell.TextAlignSource),
                CellProtect = Pick<CellProtectionLevel>(cell.CellProtect),
                RotationAlign = Pick<RotationAlign>(cell.RotationAlign),
                RotationAngle = cell.RotationAngle,
                Padding = cell.Padding,
                PaddingLeft = cell.PaddingLeft,
                DecimalPlaces = cell.DecimalPlaces,
                ShrinkToFit = Pick<OpenDocBoolean>(cell.ShrinkToFit),
                PrintContent = Pick<OpenDocBoolean>(cell.PrintContent),
            };
        }

        if (recipe.Paragraph is { } paragraph)
        {
            style.ParagraphProperties = new ParagraphProperties
            {
                TextAlign = Pick<TextAlign>(paragraph.TextAlign),
                MarginLeft = Measure(paragraph.MarginLeft),
                LineBreak = paragraph.LineBreak,
            };
        }

        if (recipe.Text is { } text)
        {
            style.TextProperties = new TextProperties
            {
                FontWeight = Pick<FontWeight>(text.FontWeight),
                FontStyle = Pick<OpenDocumentCreator.DataTypes.FontStyle>(text.FontStyle),
                FontSize = Measure(text.FontSize),
                FontFamily = text.FontFamily,
                Color = Colour(text.Color),
                BackgroundColor = Colour(text.BackgroundColor),
                Language = text.Language,
                TextUnderlineStyle = Pick<LineStyle>(text.UnderlineStyle),
                LetterSpacing = text.LetterSpacing,
            };
        }

        if (recipe.Column is { } column)
        {
            style.TableColumnProperties = new TableColumnProperties
            {
                ColumnWidth = Measure(column.ColumnWidth),
                UseOptimalColumnWidth = Pick<OpenDocBoolean>(column.UseOptimal),
                BreakBefore = Pick<BreakValue>(column.BreakBefore),
                RelativeColumnWidth = column.RelativeColumnWidth,
            };
        }

        if (recipe.Row is { } row)
        {
            style.TableRowProperties = new TableRowProperties
            {
                RowHeight = Measure(row.RowHeight),
                MinRowHeight = Measure(row.MinRowHeight),
                UseOptimalRowHeight = Pick<OpenDocBoolean>(row.UseOptimal),
                BackgroundColor = Colour(row.BackgroundColor),
                BreakBefore = Pick<BreakValue>(row.BreakBefore),
            };
        }

        if (recipe.Graphic is { } graphic)
        {
            style.GraphicProperties = new GraphicProperties
            {
                Fill = Pick<FillValue>(graphic.Fill),
                FillColor = Colour(graphic.FillColor),
                Stroke = Pick<StrokeValue>(graphic.Stroke) ?? StrokeValue.None,
                StrokeWidth = Measure(graphic.StrokeWidth),
                StrokeColor = Colour(graphic.StrokeColor),
                StrokeOpacity = graphic.Opacity,
            };
        }

        return style;
    }

    /// <summary>
    /// Turns a recipe's enum index into a member. The index is reduced modulo the number of
    /// members, so the recipe reaches all of them without naming the enum.
    /// </summary>
    /// <typeparam name="T">the enum</typeparam>
    /// <param name="index">the index, or null to leave the property unset</param>
    /// <returns>the member, or null</returns>
    private static T? Pick<T>(int? index)
        where T : struct, Enum
    {
        if (index is null)
        {
            return null;
        }

        var values = Enum.GetValues<T>();
        return values[index.Value % values.Length];
    }

    private static Measurement? Measure(MeasureRecipe? measure)
    {
        if (measure is null)
        {
            return null;
        }

        var units = Enum.GetValues<Unit>();
        return new Measurement(measure.Value, units[measure.Unit % units.Length]);
    }

    private static Color? Colour(ColorRecipe? colour)
        => colour is null ? null
            : colour.Transparent ? new Color(true)
            : new Color(colour.Red, colour.Green, colour.Blue);

    private static CompoundLine? Line(LineRecipe? line)
    {
        if (line is null)
        {
            return null;
        }

        var styles = Enum.GetValues<LineStyle>();
        return new CompoundLine(
            Measure(line.Width)!.Value,
            styles[line.Style % styles.Length],
            Colour(line.Color)!.Value);
    }

    /// <summary>
    /// Runs a step that either version may reject. A rejection is not in itself a difference; the
    /// comparison only cares whether the two versions end up with the same file, and a step that
    /// both refuse leaves both files alike. A step only one of them refuses shows up as a
    /// difference in the document they go on to produce.
    /// </summary>
    /// <param name="step">the step to run</param>
    private static void Attempt(Action step)
    {
        try
        {
            step();
        }
        catch (InvalidOperationException)
        {
        }
        catch (ArgumentException)
        {
        }
        catch (IndexOutOfRangeException)
        {
        }
    }
}
