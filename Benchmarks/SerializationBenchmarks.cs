using BenchmarkDotNet.Attributes;

namespace OpenDocumentCreator.Benchmarks;

/// <summary>
/// Cost of turning a document into a saved file.
///
/// Every element goes through the reflection driven property walk in
/// <see cref="OpenDocumentWritable"/>, so this covers the per element serialization cost as well
/// as the zipping.
/// </summary>
// Deliberately on the default job rather than ShortRunJob. ShortRunJob runs three iterations,
// which on cases this fast produced confidence intervals wider than the mean; the full job costs
// a few minutes more across the suite and is roughly fifty times tighter.
[MemoryDiagnoser]
public class SerializationBenchmarks
{
    /// <summary>How many rows the saved grid has.</summary>
    [Params(500, 2000)]
    public int Rows { get; set; }

    /// <summary>
    /// Saves a freshly built grid.
    ///
    /// The document is rebuilt for every invocation on purpose: FinishColumns mutates the table
    /// while saving, so saving the same document twice does not measure the same work
    /// (see the note in the pull request that introduced these benchmarks).
    /// </summary>
    /// <returns>the number of bytes written, so nothing is optimized away</returns>
    [Benchmark]
    public long SaveGrid()
    {
        var doc = new OpenDocumentSpreadsheet();
        var ag = new AutoGrid(doc, "T", Rows, 5, "20mm");
        doc.Tables.Add(ag);
        for (var y = 0; y < Rows; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                ag.WriteCell(x, y, "v");
            }
        }

        using var mem = new MemoryStream();
        doc.Save(mem, leaveOpen: true).GetAwaiter().GetResult();
        return mem.Length;
    }
}
