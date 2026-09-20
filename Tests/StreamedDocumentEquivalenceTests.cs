using System.IO.Compression;
using System.Xml.Linq;

namespace OpenDocumentCreator.Tests;

/// <summary>
/// The whole content.xml written by the streaming path has to match what the element model
/// produces, for real documents rather than for individual elements.
/// </summary>
[TestClass]
public sealed class StreamedDocumentEquivalenceTests
{
    private static async Task<string> SavedContentXml(OpenDocumentSpreadsheet doc)
    {
        using MemoryStream mem = new();
        await doc.Save(mem, leaveOpen: true);

        mem.Position = 0;
        using var zip = new ZipArchive(mem, ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("content.xml")!.Open());

        // The part starts with a UTF-8 byte order mark, as it always has. Parsing a string does
        // not accept one, so drop it here; a consumer reading the bytes is unaffected.
        return (await reader.ReadToEndAsync()).TrimStart('﻿');
    }

    /// <summary>
    /// Builds the document twice and renders one copy each way.
    /// </summary>
    /// <param name="build">builds a fresh, identical document each time it is called</param>
    /// <remarks>
    /// Two copies rather than one, because FinishColumns rewrites the last column's repeat count
    /// while serializing, so rendering the same document twice does not do the same work.
    /// </remarks>
    private static async Task AssertStreamedMatchesModel(Func<OpenDocumentSpreadsheet> build)
    {
        var expected = build().BuildContentFileForTesting().ToString(SaveOptions.DisableFormatting);
        var actual = await SavedContentXml(build());

        // Compare the root elements: a parsed document also carries its declaration, which the
        // element model's ToString does not render, and that is not a content difference.
        Assert.IsTrue(
            XNode.DeepEquals(XDocument.Parse(expected).Root, XDocument.Parse(actual).Root),
            "the streamed content.xml must match the element model." + Environment.NewLine +
            "model:    " + Truncate(expected) + Environment.NewLine +
            "streamed: " + Truncate(actual));
    }

    private static string Truncate(string value) => value.Length <= 900 ? value : value[..900] + " ...";

    private static OpenDocumentSpreadsheet Grid(int rows, int columns, Action<AutoGrid>? fill = null)
    {
        var doc = new OpenDocumentSpreadsheet();
        var ag = new AutoGrid(doc, "Export", rows, columns, "20mm");
        doc.Tables.Add(ag);
        fill?.Invoke(ag);
        return doc;
    }

    [TestMethod]
    public Task EmptyGridMatchesTest() => AssertStreamedMatchesModel(() => Grid(3, 3));

    [TestMethod]
    public Task TextAndNumbersMatchTest() => AssertStreamedMatchesModel(() => Grid(4, 4, ag =>
    {
        ag.WriteCell(0, 0, "plain");
        ag.WriteCell(1, 0, "two  spaces");
        ag.WriteCell(2, 0, "line\nbreak");
        ag.WriteCell(3, 0, 1234.5);
        ag.WriteCell(0, 1, 0.1);
    }));

    [TestMethod]
    public Task CharactersNeedingEscapingMatchTest() => AssertStreamedMatchesModel(() => Grid(3, 3, ag =>
    {
        ag.WriteCell(0, 0, "a & b");
        ag.WriteCell(1, 0, "<tag> \"quoted\" 'apos'");
        ag.WriteCell(2, 0, "umlauts: aou, emoji: check");
        ag.WriteCell(0, 1, "tab	and  double  spaces");
    }));

    [TestMethod]
    public Task LinkCellMatchesTest()
        => AssertStreamedMatchesModel(() => Grid(2, 2, ag => ag.WriteCell(0, 0, new Uri("https://example.invalid/a/"))));

    [TestMethod]
    public Task StylesAndSpansMatchTest() => AssertStreamedMatchesModel(() =>
    {
        var doc = new OpenDocumentSpreadsheet();
        var style = new Styles.OpenDocumentStyle { Name = "ce_x", Family = DataTypes.StyleFamily.TableCell };
        doc.Styles.Add(style.Name, style);

        var ag = new AutoGrid(doc, "Export", 4, 4, "20mm");
        doc.Tables.Add(ag);
        ag.WriteCell(0, 0, "styled", style);
        ag.SetCellSpan(1, 1, 2, 2);
        ag.WriteRowStyle(2, style);
        return doc;
    });

    [TestMethod]
    public Task SeveralTablesMatchTest() => AssertStreamedMatchesModel(() =>
    {
        var doc = new OpenDocumentSpreadsheet();
        var first = new AutoGrid(doc, "One", 2, 2, "20mm");
        var second = new AutoGrid(doc, "Two", 3, 5, "30mm");
        doc.Tables.Add(first);
        doc.Tables.Add(second);
        first.WriteCell(0, 0, "a");
        second.WriteCell(4, 2, "b");
        return doc;
    });
}
