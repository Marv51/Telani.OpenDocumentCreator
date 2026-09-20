using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace OpenDocumentCreator.Tests;

/// <summary>
/// ODF declares office:value as xs:double, so a value has to survive the round trip through
/// Save with full double precision.
/// </summary>
[TestClass]
public sealed class CellValuePrecisionTests
{
    private static readonly XNamespace Office = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:office:1.0");

    private static readonly string[] ExpectedNonFinite = ["INF", "-INF", "NaN"];

    private static async Task<List<string>> SavedCellValues(OpenDocumentSpreadsheet doc)
    {
        using MemoryStream mem = new();
        await doc.Save(mem, leaveOpen: true);
        mem.Position = 0;
        using var zip = new ZipArchive(mem, ZipArchiveMode.Read);
        using var entry = zip.GetEntry("content.xml")!.Open();
        return XDocument.Load(entry)
            .Descendants()
            .Select(e => e.Attribute(Office + "value")?.Value)
            .Where(v => v is not null)
            .Select(v => v!)
            .ToList();
    }

    private static AutoGrid Grid(OpenDocumentSpreadsheet doc, int rows)
    {
        var ag = new AutoGrid(doc, "T", rows, 1, "20mm");
        doc.Tables.Add(ag);
        return ag;
    }

    [TestMethod]
    public async Task DoublesSurviveTheRoundTripTest()
    {
        double[] values = [1234567.89, 20240917123456d, 0.123456789012345, 3.14159265358979, 0.1 + 0.2];

        var doc = new OpenDocumentSpreadsheet();
        var ag = Grid(doc, values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            ag.WriteCell(0, i, values[i]);
        }

        var written = await SavedCellValues(doc);

        Assert.HasCount(values.Length, written);
        for (var i = 0; i < values.Length; i++)
        {
            Assert.AreEqual(
                values[i],
                double.Parse(written[i], CultureInfo.InvariantCulture),
                "value " + i + " came back as " + written[i]);
        }
    }

    [TestMethod]
    public async Task LargeIntegralValueIsNotMangledTest()
    {
        // 20240917123456 is past the 2^24 point where float stops representing integers exactly;
        // as a float it used to be written as 2.0240916E+13, a different number.
        var doc = new OpenDocumentSpreadsheet();
        Grid(doc, 1).WriteCell(0, 0, 20240917123456d);

        var written = await SavedCellValues(doc);

        Assert.AreEqual("20240917123456", written.Single());
    }

    [TestMethod]
    public async Task NonFiniteValuesUseTheXsdDoubleLiteralsTest()
    {
        // xs:double spells these INF, -INF and NaN. .NET would write "Infinity", which is not
        // a valid xs:double and makes the document fail schema validation.
        var doc = new OpenDocumentSpreadsheet();
        var ag = Grid(doc, 3);
        ag.WriteCell(0, 0, double.PositiveInfinity);
        ag.WriteCell(0, 1, double.NegativeInfinity);
        ag.WriteCell(0, 2, double.NaN);

        var written = await SavedCellValues(doc);

        CollectionAssert.AreEqual(ExpectedNonFinite, written);
    }

    [TestMethod]
    public async Task FormulaResultKeepsFullPrecisionTest()
    {
        var doc = new OpenDocumentSpreadsheet();
        Grid(doc, 1).WriteCell(0, 0, new OpenDocumentCell(1234567.89) { Formula = "of:=SUM([.A1:.A2])" });

        var written = await SavedCellValues(doc);

        Assert.AreEqual("1234567.89", written.Single());
    }

    [TestMethod]
    public async Task FloatInputStillWorksTest()
    {
        // float widens implicitly, so callers passing a float keep compiling and keep their value.
        var doc = new OpenDocumentSpreadsheet();
        Grid(doc, 1).WriteCell(0, 0, 1.5f);

        var written = await SavedCellValues(doc);

        Assert.AreEqual("1.5", written.Single());
    }
}
