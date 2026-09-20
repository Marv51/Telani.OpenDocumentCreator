using BenchmarkDotNet.Attributes;

namespace OpenDocumentCreator.Benchmarks;

/// <summary>
/// Cost of reaching a cell by its position in a row.
///
/// AutoGrid indexes a row on every write. <see cref="WriteToLastColumn"/> keeps the number of
/// writes fixed and varies only how wide the row is, so anything that grows with
/// <see cref="Width"/> is the cost of getting to the cell rather than of writing it.
/// <see cref="WriteToFirstColumn"/> is the same work at position zero and should not move at all,
/// which is what makes the first one readable.
/// </summary>
// Deliberately on the default job rather than ShortRunJob. These cases run in microseconds, so
// the full job costs a few minutes for the whole class and buys roughly fifty times tighter
// confidence intervals; ShortRunJob's three iterations put an error bar wider than the mean on
// some of these.
[MemoryDiagnoser]
public class RowIndexingBenchmarks
{
    /// <summary>How many columns the table has.</summary>
    [Params(200, 500, 1000)]
    public int Width { get; set; }

    private const int Writes = 20_000;

    private AutoGrid grid = null!;

    /// <summary>Builds the table once; the benchmarks only write into cells that already exist.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var doc = new OpenDocumentSpreadsheet();
        grid = new AutoGrid(doc, "Export", 50, Width, "20mm");
        doc.Tables.Add(grid);
    }

    /// <summary>Writes a fixed number of cells at the far end of the row.</summary>
    [Benchmark]
    public void WriteToLastColumn()
    {
        var x = Width - 1;
        for (var i = 0; i < Writes; i++)
        {
            grid.WriteCell(x, 0, "v");
        }
    }

    /// <summary>The same writes at position zero, as a control.</summary>
    [Benchmark(Baseline = true)]
    public void WriteToFirstColumn()
    {
        for (var i = 0; i < Writes; i++)
        {
            grid.WriteCell(0, 0, "v");
        }
    }

    /// <summary>Filling one whole row, which is what a wide export does per row.</summary>
    /// <returns>the grid, so nothing is optimized away</returns>
    [Benchmark]
    public AutoGrid FillOneWideRow()
    {
        for (var x = 0; x < Width; x++)
        {
            grid.WriteCell(x, 1, "v");
        }
        return grid;
    }
}
