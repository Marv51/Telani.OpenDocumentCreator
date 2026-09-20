using System.IO.Compression;
using System.Xml.Linq;

namespace OpenDocumentCreator.Tests;

/// <summary>
/// Runs of spaces are carried by text:s elements rather than by literal whitespace. The scan that
/// produces them was rewritten to write straight to the writer, so these pin its edges: runs at
/// the start, in the middle and at the end, single spaces, and the odd lengths around the
/// boundary between "literal" and "counted".
/// </summary>
[TestClass]
public sealed class SpaceEncodingTests
{
    private static readonly XNamespace TextNs = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:text:1.0");

    private static async Task<XElement> WriteAndReadParagraph(string content)
    {
        var doc = new OpenDocumentSpreadsheet();
        var ag = new AutoGrid(doc, "T", 2, 2, "20mm");
        doc.Tables.Add(ag);
        ag.WriteCell(0, 0, content);

        using MemoryStream mem = new();
        await doc.Save(mem, leaveOpen: true);
        mem.Position = 0;
        using var zip = new ZipArchive(mem, ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("content.xml")!.Open());
        var xml = (await reader.ReadToEndAsync()).TrimStart('﻿');

        // PreserveWhitespace: a leading space is a whitespace only text node, which the parser
        // would otherwise drop - the file itself has it either way.
        return XDocument.Parse(xml, LoadOptions.PreserveWhitespace).Descendants(TextNs + "p").First();
    }

    /// <summary>The paragraph rendered as "text, then a space run of n, then text".</summary>
    private static string Describe(XElement paragraph)
    {
        var parts = new List<string>();
        foreach (var node in paragraph.Nodes())
        {
            if (node is XText text)
            {
                parts.Add("'" + text.Value + "'");
            }
            else if (node is XElement e && e.Name == TextNs + "s")
            {
                parts.Add("s" + (e.Attribute(TextNs + "c")?.Value ?? "?"));
            }
        }
        return string.Join("+", parts);
    }

    [TestMethod]
    public async Task SingleSpacesStayLiteralTest()
        => Assert.AreEqual("'a b c'", Describe(await WriteAndReadParagraph("a b c")));

    [TestMethod]
    public async Task RunInTheMiddleIsCountedTest()
        => Assert.AreEqual("'a '+s1+'b'", Describe(await WriteAndReadParagraph("a  b")));

    [TestMethod]
    public async Task LongerRunIsCountedTest()
        => Assert.AreEqual("'a '+s3+'b'", Describe(await WriteAndReadParagraph("a    b")));

    [TestMethod]
    public async Task RunAtTheStartIsCountedTest()
        => Assert.AreEqual("' '+s1+'a'", Describe(await WriteAndReadParagraph("  a")));

    [TestMethod]
    public async Task SeveralRunsAreEachCountedTest()
        => Assert.AreEqual("'a '+s1+'b '+s2+'c'", Describe(await WriteAndReadParagraph("a  b   c")));

    [TestMethod]
    public async Task RunAtTheEndKeepsOneSpaceTest()
    {
        // A trailing run is written as the single space that precedes it and nothing more, which
        // is what this produced before. Pinned because it is the one asymmetric case.
        Assert.AreEqual("'a '", Describe(await WriteAndReadParagraph("a   ")));
    }

    [TestMethod]
    public async Task TextWithoutRunsIsUntouchedTest()
        => Assert.AreEqual("'hello world'", Describe(await WriteAndReadParagraph("hello world")));

    [TestMethod]
    public async Task RunsAroundEscapedCharactersTest()
        => Assert.AreEqual("'a & '+s1+'<b>'", Describe(await WriteAndReadParagraph("a &  <b>")));
}
