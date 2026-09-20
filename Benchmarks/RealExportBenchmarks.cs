using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using OpenDocumentCreator.DataTypes;
using OpenDocumentCreator.Styles;

namespace OpenDocumentCreator.Benchmarks;

/// <summary>
/// An export at the size this library is used at in practice.
///
/// Only the shape is taken from a real document, never its contents: one table, 500 columns,
/// 1409 rows, about 705,000 cells, of which roughly 93% carry a style but no content, about
/// 45,000 hold text or a number, and a few hundred span several columns or rows. That produces a
/// content.xml of about 45 MB. Every value here is made up.
///
/// The point of this one is not to isolate anything. It is to say how long the library takes for
/// a document of the size people actually export, so that time can be told apart from the time an
/// application spends producing the data.
/// </summary>
[SimpleJob(warmupCount: 1, iterationCount: 8)]
[MemoryDiagnoser]
public class RealExportBenchmarks
{
    private const int Rows = 1409;
    private const int Columns = 500;

    /// <summary>One in this many cells carries content rather than only a style.</summary>
    private const int FilledEvery = 16;

    private OpenDocumentSpreadsheet prepared = null!;

    private static OpenDocumentStyle[] CellStyles(OpenDocumentSpreadsheet doc)
    {
        var styles = new OpenDocumentStyle[8];
        for (var i = 0; i < styles.Length; i++)
        {
            var name = "ce_" + i;
            styles[i] = new OpenDocumentStyle
            {
                Name = name,
                Family = StyleFamily.TableCell,
                TableCellProperties = new TableCellProperties
                {
                    BackgroundColor = new Color((byte)(200 + i), 200, 200),
                },
            };
            doc.Styles.Add(name, styles[i]);
        }
        return styles;
    }

    private static OpenDocumentSpreadsheet BuildDocument()
    {
        var doc = new OpenDocumentSpreadsheet("Benchmark");
        var styles = CellStyles(doc);

        var ag = new AutoGrid(doc, "Export", Rows, Columns, "20mm");
        doc.Tables.Add(ag);

        var counter = 0;
        for (var y = 0; y < Rows; y++)
        {
            for (var x = 0; x < Columns; x++)
            {
                var style = styles[(x + y) % styles.Length];

                if (counter++ % FilledEvery == 0)
                {
                    if (counter % 3 == 0)
                    {
                        ag.WriteCell(x, y, 1234.5 + x, style);
                    }
                    else
                    {
                        ag.WriteCell(x, y, "value " + x, style);
                    }
                }
                else
                {
                    // styled but empty, which is most of a real matrix export
                    ag.WriteCell(x, y, new OpenDocumentCell((string?)null), style);
                }
            }
        }

        // a few merged headings
        for (var i = 0; i < 100; i++)
        {
            ag.SetCellSpan(i * 4, 0, 1, 3);
        }

        return doc;
    }

    /// <summary>Assembling the grid in memory.</summary>
    /// <returns>the document, so nothing is optimized away</returns>
    [Benchmark]
    public OpenDocumentSpreadsheet Build() => BuildDocument();

    /// <summary>Builds the document to save once, outside the measured region.</summary>
    [GlobalSetup(Target = nameof(Save))]
    public void PrepareDocument() => prepared = BuildDocument();

    /// <summary>Serializing and zipping an already built document.</summary>
    /// <returns>the number of bytes written, so nothing is optimized away</returns>
    [Benchmark]
    public long Save()
    {
        using var mem = new MemoryStream();
        prepared.Save(mem, leaveOpen: true).GetAwaiter().GetResult();
        return mem.Length;
    }

    /// <summary>The whole export, which is what an application waits for.</summary>
    /// <returns>the number of bytes written, so nothing is optimized away</returns>
    [Benchmark]
    public long BuildAndSave()
    {
        using var mem = new MemoryStream();
        BuildDocument().Save(mem, leaveOpen: true).GetAwaiter().GetResult();
        return mem.Length;
    }
}
