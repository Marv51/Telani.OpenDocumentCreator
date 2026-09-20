using OpenDocumentCreator.DataTypes;
using OpenDocumentCreator.Styles;

namespace OpenDocumentCreator.Tests;

/// <summary>
/// Column styles are found through an index rather than a scan of every style. These tests pin
/// that reuse still happens, including when the style dictionary was changed from outside.
/// </summary>
[TestClass]
public sealed class ColumnStyleReuseTests
{
    private readonly OpenDocumentSpreadsheet doc = new();

    private static int ColumnStyleCount(OpenDocumentSpreadsheet doc)
        => doc.Styles.Values.Count(s => s.Family == StyleFamily.TableColumn);

    private AutoGrid SetupAutoGrid(int columns = 5)
    {
        var ag = new AutoGrid(doc, "Test-Table", 2, columns, "20mm");
        doc.Tables.Add(ag);
        return ag;
    }

    [TestMethod]
    public void IdenticalWidthsShareOneStyleTest()
    {
        var ag = SetupAutoGrid();

        ag.SetColumnsWidth(0, "33mm", "33mm", "33mm");

        Assert.AreEqual(ag.Columns[0].StyleName, ag.Columns[1].StyleName);
        Assert.AreEqual(ag.Columns[0].StyleName, ag.Columns[2].StyleName);

        // one for the 20mm default from the constructor, one for the 33mm just set
        Assert.AreEqual(2, ColumnStyleCount(doc));
    }

    [TestMethod]
    public void DistinctWidthsGetDistinctStylesTest()
    {
        var ag = SetupAutoGrid();

        ag.SetColumnsWidth(0, "11mm", "22mm", "33mm");

        Assert.HasCount(3, new[] { ag.Columns[0].StyleName, ag.Columns[1].StyleName, ag.Columns[2].StyleName }.Distinct().ToList());
        Assert.AreEqual(4, ColumnStyleCount(doc));
    }

    [TestMethod]
    public void ConstructorWidthIsReusedBySetColumnsWidthTest()
    {
        var ag = SetupAutoGrid();

        // the grid was built with 20mm columns, so asking for 20mm again must not add a style
        ag.SetColumnsWidth(0, "20mm");

        Assert.AreEqual(1, ColumnStyleCount(doc));
    }

    [TestMethod]
    public void StyleAddedFromOutsideIsStillReusedTest()
    {
        // Styles is public and mutable, so the index has to notice that it grew and rebuild
        // before it can claim a width is missing.
        var ag = SetupAutoGrid();
        var existing = new OpenDocumentStyle
        {
            Name = "my_own_column_style",
            Family = StyleFamily.TableColumn,
            TableColumnProperties = new TableColumnProperties
            {
                BreakBefore = BreakValue.Auto,
                ColumnWidth = new Measurement(44, Unit.MM),
            },
        };
        doc.Styles.Add(existing.Name, existing);

        ag.SetColumnsWidth(0, "44mm");

        Assert.AreEqual("my_own_column_style", ag.Columns[0].StyleName, "an existing matching style must be reused, not duplicated");
    }

    [TestMethod]
    public void GeneratedNamesDoNotCollideWithUserStylesTest()
    {
        // The old naming was "auto_col_" + Styles.Count, which could land on a name a caller
        // had already used.
        doc.Styles.Add("auto_col_0", new OpenDocumentStyle { Name = "auto_col_0" });
        doc.Styles.Add("auto_col_1", new OpenDocumentStyle { Name = "auto_col_1" });

        var ag = SetupAutoGrid();
        ag.SetColumnsWidth(0, "77mm");

        Assert.HasCount(2 + 2, doc.Styles, "no style may be overwritten or lost");
        Assert.AreNotEqual("auto_col_0", ag.Columns[0].StyleName);
        Assert.AreNotEqual("auto_col_1", ag.Columns[0].StyleName);
    }

    [TestMethod]
    public void StylesRemovedFromOutsideDoNotProduceDanglingNamesTest()
    {
        var ag = SetupAutoGrid();
        ag.SetColumnsWidth(0, "55mm");
        var generated = ag.Columns[0].StyleName!;

        doc.Styles.Remove(generated);

        ag.SetColumnsWidth(1, "55mm");

        Assert.IsTrue(doc.Styles.ContainsKey(ag.Columns[1].StyleName!), "the style a column points at has to exist");
    }
}
