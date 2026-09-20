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

        var styles = new OpenDocumentStyle[recipe.CellStyleCount];
        for (var i = 0; i < styles.Length; i++)
        {
            styles[i] = new OpenDocumentStyle { Name = "ce_" + i, Family = StyleFamily.TableCell };
            doc.Styles.Add(styles[i].Name!, styles[i]);
        }

        var rowStyle = new OpenDocumentStyle { Name = "ro_x", Family = StyleFamily.TableRow };
        doc.Styles.Add(rowStyle.Name!, rowStyle);

        var ag = new AutoGrid(doc, recipe.TableName, recipe.Rows, recipe.Columns, recipe.DefaultWidth);
        doc.Tables.Add(ag);

        if (recipe.ColumnWidths.Count > 0)
        {
            ag.SetColumnsWidth(0, [.. recipe.ColumnWidths]);
        }

        foreach (var step in recipe.Cells)
        {
            var style = step.StyleIndex >= 0 && step.StyleIndex < styles.Length ? styles[step.StyleIndex] : null;

            switch (step.Kind)
            {
                case CellKind.Empty:
                    ag.WriteCell(step.X, step.Y, new OpenDocumentCell((string?)null), style);
                    break;
                case CellKind.Text:
                    ag.WriteCell(step.X, step.Y, step.Text, style);
                    break;
                case CellKind.MultiLineText:
                    ag.WriteCell(step.X, step.Y, step.Text + "\nsecond\n\nfourth", style);
                    break;
                case CellKind.TextWithSpaces:
                    ag.WriteCell(step.X, step.Y, step.Text + "  two   three   ", style);
                    break;
                case CellKind.TextNeedingEscapes:
                    ag.WriteCell(step.X, step.Y, step.Text + " & < > \" '", style);
                    break;
                case CellKind.Number:
                    ag.WriteCell(step.X, step.Y, step.Number, style);
                    break;
                case CellKind.Formula:
                    ag.WriteCell(step.X, step.Y, new OpenDocumentCell(step.Number) { Formula = "of:=SUM([.A1:.A2])" }, style);
                    break;
                case CellKind.Link:
                    ag.WriteCell(step.X, step.Y, new Uri("https://example.invalid/" + step.X + "/" + step.Y), style);
                    break;
                case CellKind.Frame:
                    ag.WriteCell(step.X, step.Y, new OpenDocumentFrame { Name = "f" + step.X + step.Y }, style);
                    break;
                default:
                    break;
            }
        }

        foreach (var span in recipe.Spans)
        {
            // Both versions reject some spans for the same reasons; what matters is that they
            // agree, which the file comparison shows.
            try
            {
                ag.SetCellSpan(span.X, span.Y, span.RowSpan, span.ColumnSpan);
            }
            catch (InvalidOperationException)
            {
            }
            catch (ArgumentException)
            {
            }
        }

        foreach (var row in recipe.StyledRows)
        {
            ag.WriteRowStyle(row, rowStyle);
        }

        // 1.0.4 has no leaveOpen and closes the stream, so the bytes are taken from the closed
        // stream. That works on both versions, so the same call serves for each.
        var mem = new MemoryStream();
        doc.Save(mem, false, "Calibri").GetAwaiter().GetResult();
        return mem.ToArray();
    }
}
