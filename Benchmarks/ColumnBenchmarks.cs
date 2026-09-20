using BenchmarkDotNet.Attributes;

namespace OpenDocumentCreator.Benchmarks;

/// <summary>
/// Cost of adding columns to a table.
///
/// <see cref="OpenDocumentTable.AddColumn(Column)"/> validates the size limit by calling
/// <see cref="OpenDocumentTable.TotalNumberOfColumns"/>, which scans and re-parses every column.
/// <see cref="AddColumnsToExistingTable"/> is the telling one: the number of added columns is
/// fixed, so any growth with <see cref="ExistingColumns"/> comes from the per-call scan.
/// </summary>
// Deliberately on the default job rather than ShortRunJob. ShortRunJob runs three iterations,
// which on cases this fast produced confidence intervals wider than the mean; the full job costs
// a few minutes more across the suite and is roughly fifty times tighter.
[MemoryDiagnoser]
public class ColumnBenchmarks
{
    /// <summary>How many columns the table already holds before the measured additions.</summary>
    [Params(500, 2000, 4000)]
    public int ExistingColumns { get; set; }

    private const int Added = 2000;

    /// <summary>
    /// Adds a fixed number of columns to a table of a given starting size.
    /// </summary>
    /// <returns>the table, so nothing is optimized away</returns>
    [Benchmark]
    public OpenDocumentTable AddColumnsToExistingTable()
    {
        var table = new OpenDocumentTable("T");

        // Fill through the Columns list so the setup is not itself dominated by AddColumn.
        for (var i = 0; i < ExistingColumns; i++)
        {
            table.Columns.Add(new Column("co1"));
        }

        for (var i = 0; i < Added; i++)
        {
            table.AddColumn(new Column("co1"));
        }
        return table;
    }

    /// <summary>
    /// Builds a table of <see cref="ExistingColumns"/> columns from scratch through the public API.
    /// </summary>
    /// <returns>the grid, so nothing is optimized away</returns>
    [Benchmark]
    public AutoGrid BuildGrid()
    {
        var doc = new OpenDocumentSpreadsheet();
        var ag = new AutoGrid(doc, "T", 1, ExistingColumns, "20mm");
        doc.Tables.Add(ag);
        return ag;
    }
}
