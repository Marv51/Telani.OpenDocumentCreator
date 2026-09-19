using System.IO.Compression;

namespace OpenDocumentCreator.Tests;

[TestClass]
public sealed class OpenDocumentSaveTests
{
    [TestMethod]
    public async Task SaveClosesTheStreamByDefaultTest()
    {
        var doc = new OpenDocumentSpreadsheet();
        var mem = new MemoryStream();

        await doc.Save(mem);

        Assert.IsFalse(mem.CanRead, "Save closes the stream unless asked to leave it open");
    }

    [TestMethod]
    public async Task SaveLeavesTheStreamOpenWhenAskedTest()
    {
        var doc = new OpenDocumentSpreadsheet();
        var mem = new MemoryStream();

        await doc.Save(mem, leaveOpen: true);

        Assert.IsTrue(mem.CanRead, "the caller keeps ownership of the stream");

        // the document is complete and readable without copying the bytes out first
        mem.Position = 0;
        using var zip = new ZipArchive(mem, ZipArchiveMode.Read);
        Assert.IsNotNull(zip.GetEntry("content.xml"));
        Assert.IsNotNull(zip.GetEntry("META-INF/manifest.xml"));
    }

    [TestMethod]
    public async Task SaveToPathStillClosesItsOwnStreamTest()
    {
        var doc = new OpenDocumentSpreadsheet();
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ods");

        try
        {
            await doc.Save(path);

            // the file is not locked afterwards
            using var zip = ZipFile.OpenRead(path);
            Assert.IsNotNull(zip.GetEntry("content.xml"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
