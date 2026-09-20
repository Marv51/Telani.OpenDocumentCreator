namespace OpenDocumentCreator.Tests;

/// <summary>
/// The grid keeps every row as long as the column list. These tests pin that invariant across
/// the paths that can grow the column count, because the padding pass is now skipped when the
/// count has not moved.
/// </summary>
[TestClass]
public sealed class AutoGridPaddingTests
{
    private readonly OpenDocumentSpreadsheet doc = new();

    private AutoGrid SetupAutoGrid(int rows = 5, int columns = 5)
    {
        var ag = new AutoGrid(doc, "Test-Table", rows, columns, "20mm");
        doc.Tables.Add(ag);
        return ag;
    }

    private static void AssertRectangular(AutoGrid ag)
    {
        foreach (var row in ag.Rows)
        {
            Assert.HasCount(ag.Columns.Count, row, "every row must have one cell per column");
        }
    }

    [TestMethod]
    public void GrowingColumnsPadsExistingRowsTest()
    {
        var ag = SetupAutoGrid();

        ag.WriteCell(10, 0, "past the right edge");

        AssertRectangular(ag);
    }

    [TestMethod]
    public void GrowingRowsAfterGrowingColumnsPadsBothTest()
    {
        var ag = SetupAutoGrid();

        ag.WriteCell(10, 0, "wide");
        ag.WriteCell(0, 10, "tall");
        ag.WriteCell(12, 12, "both");

        AssertRectangular(ag);
    }

    [TestMethod]
    public void ColumnsAddedDirectlyStillPadTheRowsTest()
    {
        // AddColumn and the Columns list are public, so the column count can grow without
        // EnsureEnoughColumns having done it. Tracking the count (rather than a flag set when
        // this class grows the columns) is what keeps this case working.
        var ag = SetupAutoGrid();

        ag.AddColumn(new Column("co1"));
        ag.AddColumn(new Column("co1"));

        Assert.HasCount(7, ag.Columns);

        ag.WriteCell(0, 0, "triggers the padding pass");

        AssertRectangular(ag);
    }

    [TestMethod]
    public void RepeatedWritesKeepTheGridRectangularTest()
    {
        var ag = SetupAutoGrid(rows: 3, columns: 3);

        for (var i = 0; i < 20; i++)
        {
            ag.WriteCell(i % 4, i % 6, "v" + i);
            AssertRectangular(ag);
        }
    }

    [TestMethod]
    public void SetColumnsWidthPastTheEdgePadsRowsTest()
    {
        var ag = SetupAutoGrid();

        ag.SetColumnsWidth(8, "30mm", "30mm");

        ag.WriteCell(0, 0, "v");

        AssertRectangular(ag);
    }

    [TestMethod]
    public void WriteRowsPastTheEdgeKeepsRowsRectangularTest()
    {
        var ag = SetupAutoGrid();

        ag.WriteRows(3, 2,
            new Row([new OpenDocumentCell("1"), new OpenDocumentCell("2"), new OpenDocumentCell("3")]),
            new Row([new OpenDocumentCell("4"), new OpenDocumentCell("5")]));

        AssertRectangular(ag);
    }
}
