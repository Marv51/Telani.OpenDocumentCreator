using System.IO.Compression;
using System.Text;

namespace OpenDocumentCreator.Tests;

/// <summary>
/// Saving must not change the document. Serializing pads the table out to the column limit,
/// which the format wants but the document does not, so that padding has to be undone again -
/// otherwise a second save counts it as real columns and writes a table that claims a handful of
/// columns while every row still holds 16384 cells.
/// </summary>
[TestClass]
public sealed class RepeatedSaveTests
{
    private static OpenDocumentSpreadsheet BuildDocument()
    {
        var doc = new OpenDocumentSpreadsheet();
        var ag = new AutoGrid(doc, "Export", 3, 3, "20mm");
        doc.Tables.Add(ag);
        ag.WriteCell(0, 0, "hi");
        ag.WriteCell(2, 1, 42.5);
        return doc;
    }

    private static async Task<byte[]> SaveToBytes(OpenDocumentSpreadsheet doc)
    {
        using MemoryStream mem = new();
        await doc.Save(mem, leaveOpen: true);
        return mem.ToArray();
    }

    private static async Task<string> ContentXml(byte[] saved)
    {
        using var mem = new MemoryStream(saved);
        using var zip = new ZipArchive(mem, ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("content.xml")!.Open());
        return (await reader.ReadToEndAsync()).TrimStart('﻿');
    }

    [TestMethod]
    public async Task SavingTwiceProducesTheSameContentTest()
    {
        var doc = BuildDocument();

        var first = await ContentXml(await SaveToBytes(doc));
        var second = await ContentXml(await SaveToBytes(doc));

        Assert.AreEqual(first, second, "saving the same document twice must produce the same content.xml");
    }

    [TestMethod]
    public async Task SavingRepeatedlyStaysStableTest()
    {
        var doc = BuildDocument();
        var expected = await ContentXml(await SaveToBytes(doc));

        for (var i = 0; i < 4; i++)
        {
            Assert.AreEqual(expected, await ContentXml(await SaveToBytes(doc)), "save number " + (i + 2) + " differs");
        }
    }

    [TestMethod]
    public async Task ColumnPaddingIsStillWrittenTest()
    {
        // The padding itself must survive the fix; it is only the write back that was wrong.
        var xml = await ContentXml(await SaveToBytes(BuildDocument()));

        var repeats = System.Text.RegularExpressions.Regex
            .Matches(xml, "table:table-column[^/]*?number-columns-repeated=\"(\\d+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.AreEqual("16382", repeats[^1], "the last column still pads the table out to the column limit");
    }

    [TestMethod]
    public async Task SavingDoesNotChangeTheColumnCountTest()
    {
        var doc = BuildDocument();
        var table = doc.Tables.First();
        var before = table.TotalNumberOfColumns();

        await SaveToBytes(doc);

        Assert.AreEqual(before, table.TotalNumberOfColumns(), "the document must look the same after saving as before");
    }

    [TestMethod]
    public async Task ColumnsCanStillBeAddedAfterSavingTest()
    {
        // Previously the table was left padded to the limit, so adding a column afterwards threw.
        var doc = BuildDocument();
        var table = doc.Tables.First();

        await SaveToBytes(doc);

        table.AddColumn(new Column("co1"));

        Assert.AreEqual(4, table.TotalNumberOfColumns());
    }

    [TestMethod]
    public async Task EditingBetweenSavesIsReflectedTest()
    {
        var doc = BuildDocument();
        var ag = (AutoGrid)doc.Tables.First();

        var first = await ContentXml(await SaveToBytes(doc));
        ag.WriteCell(1, 2, "added later");
        var second = await ContentXml(await SaveToBytes(doc));

        Assert.DoesNotContain("added later", first);
        Assert.Contains("added later", second, "a change made between saves has to show up in the second file");
    }

    [TestMethod]
    public async Task TableWithoutColumnsSavesRepeatedlyTest()
    {
        var doc = new OpenDocumentSpreadsheet();
        doc.Tables.Add(new OpenDocumentTable("Empty"));

        var first = Encoding.UTF8.GetString(await SaveToBytes(doc));
        var second = Encoding.UTF8.GetString(await SaveToBytes(doc));

        Assert.AreEqual(first.Length, second.Length, "a table with no columns must survive being saved twice");
    }
}
