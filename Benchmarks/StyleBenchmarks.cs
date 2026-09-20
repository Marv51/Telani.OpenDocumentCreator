using BenchmarkDotNet.Attributes;
using OpenDocumentCreator.DataTypes;
using OpenDocumentCreator.Styles;

namespace OpenDocumentCreator.Benchmarks;

/// <summary>
/// Cost of finding a reusable column style.
///
/// Both <see cref="AutoColumnProcessor"/> and <see cref="AutoGrid.SetColumnsWidth"/> look for an
/// existing style by scanning the whole style dictionary, so the cost of setting a fixed number
/// of widths grows with how many styles the document already holds.
/// </summary>
// Deliberately on the default job rather than ShortRunJob. ShortRunJob runs three iterations,
// which on cases this fast produced confidence intervals wider than the mean; the full job costs
// a few minutes more across the suite and is roughly fifty times tighter.
[MemoryDiagnoser]
public class StyleBenchmarks
{
    /// <summary>How many unrelated styles the document already holds.</summary>
    [Params(250, 1000, 2000)]
    public int ExistingStyles { get; set; }

    private const int Widths = 400;

    private string[] distinctWidths = [];
    private string[] repeatedWidths = [];

    /// <summary>Prepares the width arrays once, outside the measured region.</summary>
    [GlobalSetup]
    public void Setup()
    {
        // Distinct widths: every entry misses the cache and adds a style.
        distinctWidths = [.. Enumerable.Range(1, Widths).Select(i => (i + 5000) + "mm")];

        // Repeated width: every entry after the first hits an existing style, which is the
        // case that pays the full scan without adding anything.
        repeatedWidths = [.. Enumerable.Repeat("33mm", Widths)];
    }

    private AutoGrid FreshGrid()
    {
        var doc = new OpenDocumentSpreadsheet();
        var ag = new AutoGrid(doc, "T", 1, Widths, "20mm");
        doc.Tables.Add(ag);
        for (var i = 0; i < ExistingStyles; i++)
        {
            doc.Styles.Add("pad" + i, new OpenDocumentStyle { Name = "pad" + i, Family = StyleFamily.TableColumn });
        }
        return ag;
    }

    /// <summary>Sets a fixed number of distinct widths.</summary>
    /// <returns>the grid, so nothing is optimized away</returns>
    [Benchmark]
    public AutoGrid SetDistinctWidths()
    {
        var ag = FreshGrid();
        ag.SetColumnsWidth(0, distinctWidths);
        return ag;
    }

    /// <summary>Sets a fixed number of identical widths, all of which should reuse one style.</summary>
    /// <returns>the grid, so nothing is optimized away</returns>
    [Benchmark]
    public AutoGrid SetRepeatedWidth()
    {
        var ag = FreshGrid();
        ag.SetColumnsWidth(0, repeatedWidths);
        return ag;
    }
}
