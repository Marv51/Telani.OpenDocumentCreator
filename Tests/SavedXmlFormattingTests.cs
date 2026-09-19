using System.IO.Compression;
using System.Xml.Linq;

namespace OpenDocumentCreator.Tests;

/// <summary>
/// The saved XML is written without indentation. These tests pin that the whitespace is gone and,
/// more importantly, that dropping it did not change what the document says.
/// </summary>
[TestClass]
public sealed class SavedXmlFormattingTests
{
    private readonly OpenDocumentSpreadsheet doc = new();

    private async Task<string> SaveAndReadContentXml()
    {
        using MemoryStream mem = new();
        await doc.Save(mem, leaveOpen: true);

        mem.Position = 0;
        using var zip = new ZipArchive(mem, ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("content.xml")!.Open());
        return await reader.ReadToEndAsync();
    }

    private AutoGrid SetupAutoGrid()
    {
        var ag = new AutoGrid(doc, "Test-Table", 3, 3, "20mm");
        doc.Tables.Add(ag);
        return ag;
    }

    [TestMethod]
    public async Task ContentXmlIsNotIndentedTest()
    {
        var ag = SetupAutoGrid();
        ag.WriteCell(0, 0, "a");

        var xml = await SaveAndReadContentXml();

        Assert.DoesNotContain(">\n  <", xml, "no indenting whitespace between elements");
        Assert.DoesNotContain(">\r\n  <", xml, "no indenting whitespace between elements");
    }

    [TestMethod]
    public async Task CellTextSurvivesUnchangedTest()
    {
        var ag = SetupAutoGrid();
        ag.WriteCell(0, 0, "hello world");

        var xml = await SaveAndReadContentXml();
        var text = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:text:1.0");
        var paragraphs = XDocument.Parse(xml).Descendants(text + "p").Select(p => p.Value).ToList();

        Assert.Contains("hello world", paragraphs, "cell text must not be re-indented or trimmed");
    }

    [TestMethod]
    public async Task RepeatedSpacesAreStillEncodedTest()
    {
        // Runs of spaces are carried by text:s elements rather than by literal whitespace, which
        // is what makes dropping the indentation safe. Pin that they are still emitted.
        var ag = SetupAutoGrid();
        ag.WriteCell(0, 0, "two  spaces");

        var xml = await SaveAndReadContentXml();
        var text = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:text:1.0");
        var spaceRuns = XDocument.Parse(xml).Descendants(text + "s").ToList();

        Assert.HasCount(1, spaceRuns);

        var count = spaceRuns[0].Attribute(text + "c");
        Assert.IsNotNull(count);
        Assert.AreEqual("1", count.Value);
    }

    [TestMethod]
    public async Task MultiLineCellStillProducesOneParagraphPerLineTest()
    {
        var ag = SetupAutoGrid();
        ag.WriteCell(0, 0, "first\nsecond");

        var xml = await SaveAndReadContentXml();
        var text = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:text:1.0");
        var paragraphs = XDocument.Parse(xml).Descendants(text + "p").Select(p => p.Value).ToList();

        Assert.Contains("first", paragraphs);
        Assert.Contains("second", paragraphs);
    }

    [TestMethod]
    public async Task SavedDocumentIsStillWellFormedTest()
    {
        var ag = SetupAutoGrid();
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                ag.WriteCell(x, y, "c" + x + y);
            }
        }

        var xml = await SaveAndReadContentXml();

        // Parsing throws on malformed XML, which is the point of the assertion.
        var parsed = XDocument.Parse(xml);
        Assert.IsNotNull(parsed.Root);
    }
}
