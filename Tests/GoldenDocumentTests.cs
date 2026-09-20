using System.IO.Compression;
using System.Globalization;
using System.Text;
using OpenDocumentCreator.DataTypes;
using OpenDocumentCreator.Styles;

namespace OpenDocumentCreator.Tests;

/// <summary>
/// Compares a saved document against a reference file checked in beside these tests.
///
/// The other serialization tests compare one code path against another. That was a strong check
/// while the element model and the writer were separate implementations, but they are one now, so
/// those comparisons have become the writer being compared against itself and can no longer
/// notice a change in what it produces. This can: the reference was written once, by hand
/// inspection, and nothing regenerates it.
///
/// It therefore pins everything the other tests do not: attribute order, element order, the
/// spelling of every value, escaping, the byte order mark and the absence of indentation - across
/// every kind of cell the library can write.
///
/// When a change is meant to alter the output, regenerate the reference by running this test with
/// ODC_UPDATE_GOLDEN=1 and reading the resulting diff before committing it.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class GoldenDocumentTests
{
    private const string GoldenFile = "Golden/content.xml";

    /// <summary>
    /// Saves under a fixed culture.
    /// </summary>
    /// <param name="save">the work to run</param>
    /// <returns>the saved bytes</returns>
    /// <remarks>
    /// The text a numeric cell displays is formatted with the current culture, so the same
    /// document saves differently depending on the machine's locale: 1234.5678 comes out as
    /// "1234,5678" here and infinity as the symbol rather than the word. The reference would
    /// therefore only match on a machine with the locale it was recorded on. Fixing the culture
    /// makes this test portable; the underlying locale dependence is reported separately.
    /// </remarks>
    private static async Task<byte[]> UnderInvariantCulture(Func<Task<byte[]>> save)
    {
        var previousCurrent = CultureInfo.CurrentCulture;
        var previousDefault = CultureInfo.DefaultThreadCurrentCulture;

        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            return await save();
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCurrent;
            CultureInfo.DefaultThreadCurrentCulture = previousDefault;
        }
    }

    /// <summary>
    /// A document exercising every branch the cell writer has.
    /// </summary>
    /// <returns>the document to serialize</returns>
    private static OpenDocumentSpreadsheet BuildReferenceDocument()
    {
        var doc = new OpenDocumentSpreadsheet("Reference");

        var plain = new OpenDocumentStyle
        {
            Name = "ce_plain",
            Family = StyleFamily.TableCell,
            TableCellProperties = new TableCellProperties { BackgroundColor = new Color(240, 240, 240) },
        };
        doc.Styles.Add(plain.Name, plain);

        var rowStyle = new OpenDocumentStyle { Name = "ro_tall", Family = StyleFamily.TableRow };
        doc.Styles.Add(rowStyle.Name, rowStyle);

        var ag = new AutoGrid(doc, "Reference", 8, 6, "20mm");
        doc.Tables.Add(ag);
        ag.SetColumnsWidth(0, "11mm", "22mm");

        // row 0: the ordinary cases
        ag.WriteCell(0, 0, new OpenDocumentCell((string?)null), plain);   // styled but empty
        ag.WriteCell(1, 0, "plain text", plain);
        ag.WriteCell(2, 0, "a & b < c > d \" e ' f", plain);              // every escapable character
        ag.WriteCell(3, 0, "two  spaces  and   three", plain);            // text:s runs
        ag.WriteCell(4, 0, "trailing run   ", plain);                     // the asymmetric case
        ag.WriteCell(5, 0, " leading run", plain);

        // row 1: numbers
        ag.WriteCell(0, 1, 1234.5678, plain);
        ag.WriteCell(1, 1, 20240917123456d, plain);                       // large integral value
        ag.WriteCell(2, 1, 0.1 + 0.2, plain);                             // awkward binary fraction
        ag.WriteCell(3, 1, double.PositiveInfinity, plain);               // xs:double INF
        ag.WriteCell(4, 1, double.NegativeInfinity, plain);
        ag.WriteCell(5, 1, double.NaN, plain);

        // row 2: formulas
        ag.WriteCell(0, 2, new OpenDocumentCell(42.5) { Formula = "of:=SUM([.A1:.A9])" }, plain);
        ag.WriteCell(1, 2, new OpenDocumentCell("text result") { Formula = "of:=CONCAT(\"a\";\"b\")" }, plain);

        // row 3: links
        ag.WriteCell(0, 3, new Uri("https://example.invalid/a/"), plain);
        ag.WriteCell(1, 3, new OpenDocumentCell(new Uri("https://example.invalid/b")) { Content = "labelled" }, plain);

        // row 4: multi line text, one paragraph per line
        ag.WriteCell(0, 4, "first\nsecond\nthird", plain);
        ag.WriteCell(1, 4, new OpenDocumentCell("keep\n\nthe gap") { EmptyLines = EmptyLineHandling.Preserve }, plain);
        ag.WriteCell(2, 4, new OpenDocumentCell("\ntrim ends\n") { EmptyLines = EmptyLineHandling.TrimEnds }, plain);

        // row 5: a frame holding an image
        var imagePath = doc.AddImageResource([0x89, 0x50, 0x4E, 0x47], "swatch.png");
        ag.WriteCell(0, 5, new OpenDocumentFrame
        {
            Name = "frame1",
            X = new Measurement(1, Unit.MM),
            Y = new Measurement(2, Unit.MM),
            Width = new Measurement(10, Unit.MM),
            Height = new Measurement(5, Unit.MM),
            Image = new OpenDocumentImage { Href = imagePath },
        }, plain);

        // row 6: a spanned cell, which covers the cells to its right and below
        ag.WriteCell(0, 6, "spans", plain);
        ag.SetCellSpan(0, 6, 2, 3);

        // row 7: a styled row and a repeated cell
        ag.WriteRowStyle(7, rowStyle);
        ag.WriteCell(0, 7, new OpenDocumentCell("repeated") { NumberColumnsRepeated = 4 }, plain);

        return doc;
    }

    private static async Task<byte[]> SaveContentXml(OpenDocumentSpreadsheet doc)
    {
        using MemoryStream mem = new();
        await doc.Save(mem, leaveOpen: true);

        mem.Position = 0;
        using var zip = new ZipArchive(mem, ZipArchiveMode.Read);
        using var entry = zip.GetEntry("content.xml")!.Open();
        using var copy = new MemoryStream();
        await entry.CopyToAsync(copy);
        return copy.ToArray();
    }

    [TestMethod]
    public async Task SavedContentMatchesTheReferenceTest()
    {
        var actual = await UnderInvariantCulture(() => SaveContentXml(BuildReferenceDocument()));

        if (Environment.GetEnvironmentVariable("ODC_UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GoldenFile)!);
            await File.WriteAllBytesAsync(GoldenFile, actual);
            Assert.Inconclusive("Reference regenerated at " + Path.GetFullPath(GoldenFile) + ". Read the diff before committing it.");
            return;
        }

        Assert.IsTrue(File.Exists(GoldenFile), "the reference file is missing: " + Path.GetFullPath(GoldenFile));
        var expected = await File.ReadAllBytesAsync(GoldenFile);

        if (!expected.AsSpan().SequenceEqual(actual))
        {
            var actualPath = Path.Combine(Path.GetTempPath(), "odc-actual-content.xml");
            await File.WriteAllBytesAsync(actualPath, actual);

            Assert.AreEqual(
                Encoding.UTF8.GetString(expected),
                Encoding.UTF8.GetString(actual),
                "the saved content.xml no longer matches the reference. The actual output is at " + actualPath +
                ". If the change is intended, rerun with ODC_UPDATE_GOLDEN=1 and review the diff.");
        }
    }

    [TestMethod]
    public async Task ReferenceStartsWithTheByteOrderMarkTest()
    {
        var actual = await UnderInvariantCulture(() => SaveContentXml(BuildReferenceDocument()));
        Assert.AreEqual("EFBBBF", Convert.ToHexString(actual.Take(3).ToArray()));
    }

    [TestMethod]
    public async Task SavingTheReferenceTwiceIsStableTest()
    {
        var doc = BuildReferenceDocument();

        var first = await UnderInvariantCulture(() => SaveContentXml(doc));
        var second = await UnderInvariantCulture(() => SaveContentXml(doc));

        Assert.AreEqual(Encoding.UTF8.GetString(first), Encoding.UTF8.GetString(second));
    }
}
