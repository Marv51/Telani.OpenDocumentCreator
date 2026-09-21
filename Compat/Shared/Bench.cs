using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using OpenDocumentCreator;
using OpenDocumentCreator.DataTypes;
using OpenDocumentCreator.Styles;

namespace OdcCompat;

/// <summary>
/// Builds and saves one document, so the released library and the working copy can be measured
/// doing the same work.
///
/// This one file is compiled into both benchmark projects, against a different version of the
/// library in each, for the same reason Build.cs is: the two sides are the same source, so the
/// difference between the numbers is the difference between the libraries.
///
/// Building and saving are timed together, on a document constructed inside the benchmark. That
/// is the figure a caller sees, and it also sidesteps 1.0.4 not being safe to save twice - the
/// bug #20 described - which would otherwise make repeated iterations meaningless.
/// </summary>
[MemoryDiagnoser]
public class ExportBenchmark
{
    /// <summary>
    /// Rows by columns. The wide shape is a realistic export: many columns, most cells empty but
    /// styled. The tall one is the other common shape.
    /// </summary>
    [Params("1000x100", "5000x20")]
    public string Shape { get; set; } = "1000x100";

    private int rows;
    private int columns;

    [GlobalSetup]
    public void Setup()
    {
        var parts = Shape.Split('x');
        rows = int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
        columns = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
    }

    [Benchmark]
    public long BuildAndSave()
    {
        var doc = new OpenDocumentSpreadsheet("bench");

        var styles = new OpenDocumentStyle[4];
        for (var i = 0; i < styles.Length; i++)
        {
            styles[i] = new OpenDocumentStyle
            {
                Name = "ce_" + i,
                Family = StyleFamily.TableCell,
                TableCellProperties = new TableCellProperties
                {
                    Border = new CompoundLine(new Measurement(0.74m, Unit.PT), LineStyle.Solid, new Color(0, 0, 0)),
                    VerticalAlign = VerticalAlign.Middle,
                },
            };
            doc.Styles.Add(styles[i].Name!, styles[i]);
        }

        var grid = new AutoGrid(doc, "Sheet1", rows, columns, "20mm");
        doc.Tables.Add(grid);

        grid.SetColumnsWidth(0, [.. Enumerable.Range(0, Math.Min(columns, 20)).Select(i => (10 + (i % 5)) + "mm")]);

        // Roughly one cell in eight carries text and the rest are empty but styled, which is what
        // a real export looks like.
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                var style = styles[(x + y) % styles.Length];

                if ((x + y) % 8 == 0)
                {
                    grid.WriteCell(x, y, "row " + y + " col " + x, style);
                }
                else if ((x + y) % 23 == 0)
                {
                    grid.WriteCell(x, y, (x * 1.5) + y, style);
                }
                else
                {
                    grid.WriteCell(x, y, new OpenDocumentCell((string?)null), style);
                }
            }
        }

        using var mem = new KeepOpenStream();
        doc.Save(mem, false, "Calibri").GetAwaiter().GetResult();
        return mem.Length;
    }

    /// <summary>
    /// A stream that ignores being closed. The released version closes whatever stream it is
    /// handed - it has no leaveOpen - so the length has to be readable afterwards. Reading it back
    /// with ToArray would work too, but that copies the whole output and would show up in the
    /// allocation figures.
    /// </summary>
    private sealed class KeepOpenStream : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
        }
    }
}

internal static class BenchProgram
{
    private static void Main(string[] args) => BenchmarkRunner.Run<ExportBenchmark>(null, args);
}
