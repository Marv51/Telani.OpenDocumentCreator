using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace OpenDocumentCreator.Benchmarks;

/// <summary>
/// A realistically sized export, split into its two phases.
///
/// <see cref="Build"/> is everything up to having the grid in memory, <see cref="Save"/> is
/// turning that into a zipped file, and <see cref="BuildAndSave"/> is the whole thing. Comparing
/// the first two answers the question the other benchmarks cannot: whether the cost of an export
/// sits in assembling the document or in serializing it.
///
/// <see cref="Save"/> rebuilds the document in an iteration setup, outside the measured region.
/// That is not just tidiness: FinishColumns mutates the table while saving, so saving the same
/// document twice does not do the same work.
/// </summary>
// Monitoring rather than the usual throughput strategy: the save benchmark needs a fresh
// document per invocation, and an iteration setup pins the invocation count to one. Monitoring
// is built for that shape - it measures whole iterations instead of trying to batch them.
[SimpleJob(RunStrategy.Monitoring, warmupCount: 2, iterationCount: 12)]
[MemoryDiagnoser]
public class LargeDocumentBenchmarks
{
    /// <summary>
    /// The shape of the exported grid, as "rows x columns".
    ///
    /// 1000x600 is the shape of a real export this library is used for; the two tall-and-narrow
    /// shapes are there because a wide grid and a tall grid stress different code.
    /// </summary>
    [Params("1000x15", "5000x15", "1000x600")]
    public string Shape { get; set; } = "1000x15";

    private int rows;
    private int columns;

    /// <summary>Splits the shape parameter into its two numbers.</summary>
    [GlobalSetup]
    public void ParseShape()
    {
        var parts = Shape.Split('x');
        rows = int.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
        columns = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
    }

    private OpenDocumentSpreadsheet prepared = null!;

    private static OpenDocumentSpreadsheet BuildDocument(int rows, int columns)
    {
        var doc = new OpenDocumentSpreadsheet();
        var ag = new AutoGrid(doc, "Export", rows, columns, "20mm");
        doc.Tables.Add(ag);

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                ag.WriteCell(x, y, "cell");
            }
        }
        return doc;
    }

    /// <summary>Builds the grid and fills every cell.</summary>
    /// <returns>the document, so nothing is optimized away</returns>
    [Benchmark]
    public OpenDocumentSpreadsheet Build() => BuildDocument(rows, columns);

    /// <summary>Rebuilds the document before each measured save, outside the measured region.</summary>
    [IterationSetup(Target = nameof(Save))]
    public void PrepareDocument() => prepared = BuildDocument(rows, columns);


    /// <summary>Serializes and zips an already built document.</summary>
    /// <returns>the number of bytes written, so nothing is optimized away</returns>
    [Benchmark]
    public long Save()
    {
        using var mem = new MemoryStream();
        prepared.Save(mem, leaveOpen: true).GetAwaiter().GetResult();
        return mem.Length;
    }

    /// <summary>Builds and saves, which is what an export actually costs.</summary>
    /// <returns>the number of bytes written, so nothing is optimized away</returns>
    [Benchmark]
    public long BuildAndSave()
    {
        using var mem = new MemoryStream();
        BuildDocument(rows, columns).Save(mem, leaveOpen: true).GetAwaiter().GetResult();
        return mem.Length;
    }
}
