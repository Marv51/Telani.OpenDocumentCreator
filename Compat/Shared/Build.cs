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

        var cellStyles = new OpenDocumentStyle[recipe.CellStyleCount];
        for (var i = 0; i < cellStyles.Length; i++)
        {
            cellStyles[i] = new OpenDocumentStyle { Name = "ce_" + i, Family = StyleFamily.TableCell };
            doc.Styles.Add(cellStyles[i].Name!, cellStyles[i]);
        }

        var columnStyles = new OpenDocumentStyle[recipe.ColumnStyleCount];
        for (var i = 0; i < columnStyles.Length; i++)
        {
            columnStyles[i] = new OpenDocumentStyle { Name = "co_" + i, Family = StyleFamily.TableColumn };
            doc.Styles.Add(columnStyles[i].Name!, columnStyles[i]);
        }

        var rowStyle = new OpenDocumentStyle { Name = "ro_x", Family = StyleFamily.TableRow };
        doc.Styles.Add(rowStyle.Name!, rowStyle);

        foreach (var table in recipe.Tables)
        {
            BuildTable(doc, table, cellStyles, columnStyles, rowStyle);
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
        OpenDocumentStyle rowStyle)
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
            ag.WriteRowStyle(row, rowStyle);
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

            case BulkKind.WriteRow:
                Attempt(() => ag.WriteRow(bulk.X, bulk.Y, MakeRow(bulk.Values[0], style)));
                break;

            case BulkKind.WriteRowsArray:
                Attempt(() => ag.WriteRows(bulk.X, bulk.Y, [.. bulk.Values.Select(r => MakeRow(r, style))]));
                break;

            case BulkKind.WriteRowsEnumerable:
                // Cast so the IEnumerable overload is taken rather than the params array one: the
                // two reach the grid differently.
                Attempt(() => ag.WriteRows(bulk.X, bulk.Y, bulk.Values.Select(r => MakeRow(r, style))));
                break;

            default:
                break;
        }
    }

    private static Row MakeRow(IReadOnlyList<string> values, OpenDocumentStyle? style)
    {
        var row = style is null ? new Row() : new Row(style);
        foreach (var value in values)
        {
            row.Add(new OpenDocumentCell(value));
        }
        return row;
    }

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
