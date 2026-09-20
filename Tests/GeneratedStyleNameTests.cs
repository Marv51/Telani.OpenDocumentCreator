using OpenDocumentCreator.DataTypes;
using OpenDocumentCreator.Styles;

namespace OpenDocumentCreator.Tests;

/// <summary>
/// Generated column style names are part of the file, so changing how they are chosen changes
/// every document that has other styles in it. These pin the scheme the library has always used,
/// and the one case it could not handle.
/// </summary>
[TestClass]
public sealed class GeneratedStyleNameTests
{
    private readonly OpenDocumentSpreadsheet doc = new();

    private static OpenDocumentStyle CellStyle(string name)
        => new() { Name = name, Family = StyleFamily.TableCell };

    [TestMethod]
    public void FirstGeneratedNameIsAutoColZeroInAnEmptyDocumentTest()
    {
        var ag = new AutoGrid(doc, "T", 2, 2, "20mm");
        doc.Tables.Add(ag);

        Assert.AreEqual("auto_col_0", ag.Columns[0].StyleName);
    }

    [TestMethod]
    public void NameCountsEveryStyleNotJustColumnsTest()
    {
        // The number is the style count at the time, which includes styles of any family. A
        // document with three cell styles therefore starts its column styles at auto_col_3.
        doc.Styles.Add("ce_a", CellStyle("ce_a"));
        doc.Styles.Add("ce_b", CellStyle("ce_b"));
        doc.Styles.Add("ce_c", CellStyle("ce_c"));

        var ag = new AutoGrid(doc, "T", 2, 2, "20mm");
        doc.Tables.Add(ag);

        Assert.AreEqual("auto_col_3", ag.Columns[0].StyleName);
    }

    [TestMethod]
    public void SecondDistinctWidthTakesTheNextNumberTest()
    {
        var ag = new AutoGrid(doc, "T", 2, 3, "20mm");
        doc.Tables.Add(ag);

        ag.SetColumnsWidth(0, "44mm");

        Assert.AreEqual("auto_col_0", ag.Columns[1].StyleName, "the constructor's width keeps its style");
        Assert.AreEqual("auto_col_1", ag.Columns[0].StyleName);
    }

    [TestMethod]
    public void TakenNameIsSteppedOverRatherThanThrowingTest()
    {
        // A caller naming a style auto_col_1 itself used to make this throw when the generated
        // name collided with it.
        doc.Styles.Add("ce_a", CellStyle("ce_a"));
        doc.Styles.Add("auto_col_1", CellStyle("auto_col_1"));

        var ag = new AutoGrid(doc, "T", 2, 3, "20mm");
        doc.Tables.Add(ag);
        ag.SetColumnsWidth(0, "44mm");

        Assert.AreEqual("auto_col_2", ag.Columns[1].StyleName, "the first width takes the count, which is free");
        Assert.AreEqual("auto_col_3", ag.Columns[0].StyleName, "the next would be auto_col_3; auto_col_1 is not revisited");
        Assert.HasCount(4, doc.Styles, "nothing was overwritten");
    }

    [TestMethod]
    public void ManyTakenNamesAreAllSteppedOverTest()
    {
        for (var i = 0; i < 5; i++)
        {
            doc.Styles.Add("auto_col_" + i, CellStyle("auto_col_" + i));
        }

        var ag = new AutoGrid(doc, "T", 2, 2, "20mm");
        doc.Tables.Add(ag);

        Assert.AreEqual("auto_col_5", ag.Columns[0].StyleName);
    }

    [TestMethod]
    public void ReusedWidthDoesNotConsumeANameTest()
    {
        var ag = new AutoGrid(doc, "T", 2, 4, "20mm");
        doc.Tables.Add(ag);

        // all four columns share one style, so only one name was taken
        Assert.ContainsSingle(doc.Styles.Values.Where(s => s.Family == StyleFamily.TableColumn));

        ag.SetColumnsWidth(0, "20mm");

        Assert.ContainsSingle(doc.Styles.Values.Where(s => s.Family == StyleFamily.TableColumn), "an existing width must be reused");
    }
}
