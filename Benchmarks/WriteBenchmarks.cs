using BenchmarkDotNet.Attributes;

namespace OpenDocumentCreator.Benchmarks;

/// <summary>
/// Cost of writing single cells into a grid.
///
/// <see cref="WriteCellsIntoExistingTable"/> keeps the number of writes fixed while varying how
/// many rows the table already has, so it isolates per write work that depends on the row count.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class WriteCellBenchmarks
{
    /// <summary>How many rows the table already holds.</summary>
    [Params(1000, 4000, 8000)]
    public int ExistingRows { get; set; }

    private const int Writes = 10_000;

    private AutoGrid grid = null!;

    /// <summary>Builds the table once; the benchmark only writes into cells that already exist.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var doc = new OpenDocumentSpreadsheet();
        grid = new AutoGrid(doc, "T", ExistingRows, 3, "20mm");
        doc.Tables.Add(grid);
    }

    /// <summary>Writes a fixed number of cells into a table that never grows.</summary>
    [Benchmark]
    public void WriteCellsIntoExistingTable()
    {
        for (var i = 0; i < Writes; i++)
        {
            grid.WriteCell(0, 0, "v");
        }
    }

    /// <summary>Fills a tall grid one cell at a time, growing it as it goes.</summary>
    /// <returns>the grid, so nothing is optimized away</returns>
    [Benchmark]
    public AutoGrid FillTallGrid()
    {
        var doc = new OpenDocumentSpreadsheet();
        var ag = new AutoGrid(doc, "T", 1, 3, "20mm");
        doc.Tables.Add(ag);
        for (var y = 0; y < ExistingRows; y++)
        {
            ag.WriteCell(0, y, "v");
        }
        return ag;
    }
}

/// <summary>
/// Cost of writing whole rows.
///
/// <see cref="AutoGrid.WriteRows(int, int, IEnumerable{Row})"/> counts the sequence and then
/// enumerates it again, so a lazily produced source is materialized twice. The two benchmarks
/// differ only in whether the caller hands over a list or a lazy sequence.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class WriteRowsBenchmarks
{
    /// <summary>How many rows are written.</summary>
    [Params(500, 2000)]
    public int RowCount { get; set; }

    private static AutoGrid FreshGrid()
    {
        var doc = new OpenDocumentSpreadsheet();
        var ag = new AutoGrid(doc, "T", 1, 3, "20mm");
        doc.Tables.Add(ag);
        return ag;
    }

    private IEnumerable<Row> LazyRows()
        => Enumerable.Range(0, RowCount)
            .Select(i => new Row([new OpenDocumentCell("a"), new OpenDocumentCell("b")]));

    /// <summary>Writes rows handed over as a materialized list.</summary>
    /// <returns>the number of rows written</returns>
    [Benchmark(Baseline = true)]
    public int FromList() => FreshGrid().WriteRows(0, 0, LazyRows().ToList());

    /// <summary>Writes rows handed over as a lazily produced sequence.</summary>
    /// <returns>the number of rows written</returns>
    [Benchmark]
    public int FromLazySequence() => FreshGrid().WriteRows(0, 0, LazyRows());
}
