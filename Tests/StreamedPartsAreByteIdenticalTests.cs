using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace OpenDocumentCreator.Tests;

/// <summary>
/// Every XML part of a saved document has to come out exactly as the element model produced it,
/// down to the bytes: the declaration, the byte order mark and the encoding included. A silent
/// difference here would change every file the library writes.
/// </summary>
[TestClass]
public sealed class StreamedPartsAreByteIdenticalTests
{
    private static OpenDocumentSpreadsheet BuildDocument()
    {
        var doc = new OpenDocumentSpreadsheet("A Creator");
        var ag = new AutoGrid(doc, "Export", 3, 4, "20mm");
        doc.Tables.Add(ag);
        ag.SetColumnsWidth(0, "11mm", "22mm");
        ag.WriteCell(0, 0, "text & more");
        ag.WriteCell(1, 0, 12.5);
        ag.WriteCell(2, 1, "two  spaces");
        return doc;
    }

    private static async Task<byte[]> SavedPart(string path)
    {
        using MemoryStream mem = new();
        await BuildDocument().Save(mem, leaveOpen: true);

        mem.Position = 0;
        using var zip = new ZipArchive(mem, ZipArchiveMode.Read);
        using var entry = zip.GetEntry(path)!.Open();
        using var copy = new MemoryStream();
        await entry.CopyToAsync(copy);
        return copy.ToArray();
    }

    /// <summary>
    /// Renders a part the way the library did before: an XDocument saved with formatting
    /// disabled, which is what AddXMLEntry produced.
    /// </summary>
    private static byte[] ViaElementModel(XDocument document)
    {
        using var mem = new MemoryStream();
        document.Save(mem, SaveOptions.DisableFormatting);
        return mem.ToArray();
    }

    private static void AssertSameBytes(byte[] expected, byte[] actual, string path)
    {
        Assert.AreEqual(
            Convert.ToHexString(expected.Take(8).ToArray()),
            Convert.ToHexString(actual.Take(8).ToArray()),
            path + ": the first bytes differ, which means the byte order mark or declaration changed");

        Assert.AreEqual(
            Encoding.UTF8.GetString(expected),
            Encoding.UTF8.GetString(actual),
            path + ": the streamed part must be byte for byte what the element model produced");
    }

    [TestMethod]
    public async Task ContentXmlIsUnchangedTest()
        => AssertSameBytes(ViaElementModel(BuildDocument().BuildContentFileForTesting()), await SavedPart("content.xml"), "content.xml");

    [TestMethod]
    public async Task StylesXmlIsUnchangedTest()
        => AssertSameBytes(ViaElementModel(BuildDocument().BuildStyleFileForTesting()), await SavedPart("styles.xml"), "styles.xml");

    [TestMethod]
    public async Task MetaXmlIsUnchangedTest()
        => AssertSameBytes(ViaElementModel(BuildDocument().BuildMetaFileForTesting()), await SavedPart("meta.xml"), "meta.xml");

    [TestMethod]
    public async Task ManifestIsUnchangedTest()
        => AssertSameBytes(ViaElementModel(BuildDocument().BuildManifestForTesting()), await SavedPart("META-INF/manifest.xml"), "META-INF/manifest.xml");

    [TestMethod]
    public async Task EveryPartStartsWithTheByteOrderMarkTest()
    {
        foreach (var path in new[] { "content.xml", "styles.xml", "meta.xml", "META-INF/manifest.xml" })
        {
            var bytes = await SavedPart(path);
            Assert.AreEqual("EFBBBF", Convert.ToHexString(bytes.Take(3).ToArray()), path + " must keep its byte order mark");
        }
    }
}
