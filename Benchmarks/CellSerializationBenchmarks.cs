using System.Xml.Linq;
using BenchmarkDotNet.Attributes;

namespace OpenDocumentCreator.Benchmarks;

/// <summary>
/// The per cell step of serialization, which is the innermost loop of saving a document.
///
/// <see cref="CreateElement"/> is what the library actually does. <see cref="HandWrittenElement"/>
/// builds the same XML for the same cell without going through the wrapper object and the
/// reflection driven property walk, and exists purely to show how much of the per cell cost is
/// that machinery rather than the XML itself.
/// </summary>
// Deliberately on the default job rather than ShortRunJob. ShortRunJob runs three iterations,
// which on cases this fast produced confidence intervals wider than the mean; the full job costs
// a few minutes more across the suite and is roughly fifty times tighter.
[MemoryDiagnoser]
public class CellSerializationBenchmarks
{
    private static readonly XNamespace Table = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:table:1.0");
    private static readonly XNamespace Text = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:text:1.0");
    private static readonly XNamespace Office = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:office:1.0");

    private OpenDocumentCell textCell = null!;
    private OpenDocumentCell emptyCell = null!;

    /// <summary>Prepares the cells once, outside the measured region.</summary>
    [GlobalSetup]
    public void Setup()
    {
        textCell = new OpenDocumentCell("some cell text");
        emptyCell = new OpenDocumentCell((string?)null);
    }

    /// <summary>Serializes a text cell the way saving does.</summary>
    /// <returns>the element, so nothing is optimized away</returns>
    [Benchmark(Baseline = true)]
    public XElement CreateElement() => textCell.CreateElement();

    /// <summary>Serializes an empty cell, which is the bulk of a padded grid.</summary>
    /// <returns>the element, so nothing is optimized away</returns>
    [Benchmark]
    public XElement CreateEmptyElement() => emptyCell.CreateElement();

    /// <summary>
    /// The same XML as <see cref="CreateElement"/>, built directly. This is a floor, not a
    /// proposal: it skips the style lookup and the attribute machinery entirely.
    /// </summary>
    /// <returns>the element, so nothing is optimized away</returns>
    [Benchmark]
    public XElement HandWrittenElement()
    {
        var elem = new XElement(
            Table + "table-cell",
            new XAttribute(Table + "style-name", "ce1"),
            new XAttribute(Office + "value-type", "string"));
        elem.Add(new XElement(Text + "p", "some cell text"));
        return elem;
    }
}
