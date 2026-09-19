namespace OpenDocumentCreator.Tests;

[TestClass]
public sealed class RowTests
{
    private static Row ThreeCells() => new(
        [new OpenDocumentCell("a"), new OpenDocumentCell("b"), new OpenDocumentCell("c")]);

    [TestMethod]
    public void IndexerReadsAndWritesTest()
    {
        var row = ThreeCells();

        Assert.AreEqual("b", row[1].Content);

        row[1] = new OpenDocumentCell("replaced");

        Assert.AreEqual("replaced", row[1].Content);
        Assert.HasCount(3, row, "assigning through the indexer replaces rather than inserts");
    }

    [TestMethod]
    public void IndexerOutsideTheRowThrowsTest()
    {
        var row = ThreeCells();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = row[3]);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = row[-1]);
    }

    [TestMethod]
    public void IndexOfInsertAndRemoveAtTest()
    {
        var row = ThreeCells();
        var b = row[1];

        Assert.AreEqual(1, row.IndexOf(b));
        Assert.AreEqual(-1, row.IndexOf(new OpenDocumentCell("not in the row")));

        row.Insert(1, new OpenDocumentCell("inserted"));

        Assert.HasCount(4, row);
        Assert.AreEqual("inserted", row[1].Content);
        Assert.AreEqual(2, row.IndexOf(b), "the cells after the insert move along");

        row.RemoveAt(1);

        Assert.HasCount(3, row);
        Assert.AreEqual(1, row.IndexOf(b));
    }

    [TestMethod]
    public void ReplaceKeepsRowLengthTest()
    {
        var row = ThreeCells();

        row.Replace(0, new OpenDocumentCell("first"));

        Assert.HasCount(3, row);
        Assert.AreEqual("first", row[0].Content);
        Assert.AreEqual("b", row[1].Content, "replacing must not shift the following cells");
    }

    [TestMethod]
    public void CellsReflectsLaterChangesTest()
    {
        var row = ThreeCells();
        var cells = row.Cells;

        row.InsertCell("d");

        Assert.HasCount(4, cells, "Cells is a view on the row, not a snapshot");
        Assert.AreSame(cells, row.Cells, "Cells does not allocate a new wrapper per access");
    }

    [TestMethod]
    public void RowIsAnIListTest()
    {
        // AutoGrid indexes rows heavily; going through IList keeps that off the
        // O(n) LINQ path.
#pragma warning disable CA1859 // the interface type is exactly what this test is about
        IList<OpenDocumentCell> row = ThreeCells();
#pragma warning restore CA1859

        Assert.AreEqual("c", row[2].Content);
        Assert.AreEqual("c", row.ElementAt(2).Content);
    }

    [TestMethod]
    public void StyledRowStillInitializesTest()
    {
        var style = new Styles.OpenDocumentStyle { Name = "ro_tall" };
        var row = new Row(style);

        row.InsertCell("a");

        Assert.AreSame(style, row.Style);
        Assert.HasCount(1, row.Cells);
    }
}
