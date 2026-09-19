using System.IO.Compression;
using System.Xml.Linq;
using BenchmarkDotNet.Attributes;

namespace OpenDocumentCreator.Benchmarks;

/// <summary>
/// Splits the save phase into its parts.
///
/// Saving does two things: it materializes the whole document as an XDocument, and it writes that
/// tree out as text into a deflate stream. <see cref="FullSave"/> is both.
/// <see cref="WriteXmlUncompressed"/> and <see cref="WriteXmlToZipEntry"/> take a tree that is
/// already built and only write it, so the difference from the full save is roughly what building
/// the tree costs.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class SavePhaseBenchmarks
{
    /// <summary>Rows in the exported grid.</summary>
    [Params(2000)]
    public int Rows { get; set; }

    private const int Columns = 15;

    private XDocument contentXml = null!;

    private static OpenDocumentSpreadsheet BuildDocument(int rows)
    {
        var doc = new OpenDocumentSpreadsheet();
        var ag = new AutoGrid(doc, "Export", rows, Columns, "20mm");
        doc.Tables.Add(ag);
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < Columns; x++)
            {
                ag.WriteCell(x, y, "cell");
            }
        }
        return doc;
    }

    /// <summary>Saves once and keeps the resulting content.xml as a parsed tree.</summary>
    [GlobalSetup]
    public void Setup()
    {
        using var mem = new MemoryStream();
        BuildDocument(Rows).Save(mem, leaveOpen: true).GetAwaiter().GetResult();
        mem.Position = 0;
        using var zip = new ZipArchive(mem, ZipArchiveMode.Read);
        using var entry = zip.GetEntry("content.xml")!.Open();
        contentXml = XDocument.Load(entry);
    }

    /// <summary>Builds the tree and writes the whole archive, which is what Save does.</summary>
    /// <returns>the number of bytes written, so nothing is optimized away</returns>
    [Benchmark(Baseline = true)]
    public long FullSave()
    {
        using var mem = new MemoryStream();
        BuildDocument(Rows).Save(mem, leaveOpen: true).GetAwaiter().GetResult();
        return mem.Length;
    }

    /// <summary>Writes an already built tree as text, with no compression at all.</summary>
    /// <returns>the number of bytes written, so nothing is optimized away</returns>
    [Benchmark]
    public long WriteXmlUncompressed()
    {
        using var mem = new MemoryStream();
        contentXml.Save(mem, SaveOptions.DisableFormatting);
        return mem.Length;
    }

    /// <summary>Writes an already built tree into a deflate stream, as saving does.</summary>
    /// <returns>the number of bytes written, so nothing is optimized away</returns>
    [Benchmark]
    public long WriteXmlToZipEntry()
    {
        using var mem = new MemoryStream();
        using (var zip = new ZipArchive(mem, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("content.xml", CompressionLevel.Optimal);
            using var stream = entry.Open();
            contentXml.Save(stream, SaveOptions.DisableFormatting);
        }
        return mem.Length;
    }
}
