namespace OpenDocumentCreator.Tests;

/// <summary>
/// WriteRows used to count the sequence and then iterate it again. These tests pin that a lazily
/// produced source is now walked exactly once, and that the rows written are the rows counted.
/// </summary>
[TestClass]
public sealed class WriteRowsEnumerationTests
{
    private readonly OpenDocumentSpreadsheet doc = new();

    private AutoGrid SetupAutoGrid()
    {
        var ag = new AutoGrid(doc, "Test-Table", 1, 3, "20mm");
        doc.Tables.Add(ag);
        return ag;
    }

    [TestMethod]
    public void LazySequenceIsEnumeratedOnceTest()
    {
        var ag = SetupAutoGrid();
        var produced = 0;

        IEnumerable<Row> Rows()
        {
            for (var i = 0; i < 4; i++)
            {
                produced++;
                yield return new Row([new OpenDocumentCell("r" + i)]);
            }
        }

        var written = ag.WriteRows(0, 0, Rows());

        Assert.AreEqual(4, written);
        Assert.AreEqual(4, produced, "the sequence must be walked exactly once");
    }

    [TestMethod]
    public void RowsWrittenAreTheRowsCountedTest()
    {
        // A generator hands out a fresh Row per enumeration. If the sequence were walked twice,
        // the cells landing in the grid would come from throwaway objects counted on the first
        // pass, and a source that yields different values each time would write the second batch.
        var ag = SetupAutoGrid();
        var call = 0;

        IEnumerable<Row> Rows()
        {
            for (var i = 0; i < 3; i++)
            {
                yield return new Row([new OpenDocumentCell("pass" + call + "_" + i)]);
            }
            call++;
        }

        ag.WriteRows(0, 0, Rows());

        Assert.AreEqual("pass0_0", ag.Rows[0].ElementAt(0).Content);
        Assert.AreEqual("pass0_1", ag.Rows[1].ElementAt(0).Content);
        Assert.AreEqual("pass0_2", ag.Rows[2].ElementAt(0).Content);
    }

    [TestMethod]
    public void ListSourceStillWritesEveryRowTest()
    {
        var ag = SetupAutoGrid();

        var written = ag.WriteRows(0, 0, new List<Row>
        {
            new([new OpenDocumentCell("a")]),
            new([new OpenDocumentCell("b")]),
        });

        Assert.AreEqual(2, written);
        Assert.AreEqual("a", ag.Rows[0].ElementAt(0).Content);
        Assert.AreEqual("b", ag.Rows[1].ElementAt(0).Content);
    }

    [TestMethod]
    public void EmptySequenceWritesNothingTest()
    {
        var ag = SetupAutoGrid();

        var written = ag.WriteRows(0, 0, Enumerable.Empty<Row>());

        Assert.AreEqual(0, written);
    }
}
