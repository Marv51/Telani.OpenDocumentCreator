namespace OpenDocumentCreator.Tests;

/// <summary>
/// AddColumn now carries the column total forward instead of re-adding every column. These tests
/// pin that the reported total and the size limit still match a full recount on the paths that
/// can change the columns without going through AddColumn.
/// </summary>
[TestClass]
public sealed class OpenDocumentTableColumnCountTests
{
    private const int Limit = 16385;

    [TestMethod]
    public void TotalMatchesTheNumberOfAddedColumnsTest()
    {
        var table = new OpenDocumentTable("T");

        for (var i = 0; i < 25; i++)
        {
            table.AddColumn(new Column("co1"));
        }

        Assert.AreEqual(25, table.TotalNumberOfColumns());
    }

    [TestMethod]
    public void RepeatedColumnsCountTowardsTheTotalTest()
    {
        var table = new OpenDocumentTable("T");

        table.AddColumn(new Column("co1"));
        table.AddColumn(new Column("co1") { NumberColumnsRepeated = "10" });
        table.AddColumn(new Column("co1"));

        Assert.AreEqual(12, table.TotalNumberOfColumns());
    }

    [TestMethod]
    public void ColumnsAddedThroughTheListAreCountedTest()
    {
        // Columns is a mutable list, so it can grow without AddColumn seeing it. The carried
        // total has to notice and recount.
        var table = new OpenDocumentTable("T");
        table.AddColumn(new Column("co1"));

        table.Columns.Add(new Column("co1"));
        table.Columns.Add(new Column("co1"));

        table.AddColumn(new Column("co1"));

        Assert.AreEqual(4, table.TotalNumberOfColumns());
    }

    [TestMethod]
    public void ColumnsRemovedThroughTheListAreCountedTest()
    {
        var table = new OpenDocumentTable("T");
        for (var i = 0; i < 5; i++)
        {
            table.AddColumn(new Column("co1"));
        }

        table.Columns.RemoveAt(0);
        table.AddColumn(new Column("co1"));

        Assert.AreEqual(5, table.TotalNumberOfColumns());
    }

    [TestMethod]
    public void ExceedingTheColumnLimitStillThrowsTest()
    {
        var table = new OpenDocumentTable("T");
        table.AddColumn(new Column("co1") { NumberColumnsRepeated = Limit.ToString(System.Globalization.CultureInfo.InvariantCulture) });

        Assert.ThrowsExactly<InvalidOperationException>(() => table.AddColumn(new Column("co1")));
    }

    [TestMethod]
    public void ReachingTheLimitExactlyDoesNotThrowTest()
    {
        var table = new OpenDocumentTable("T");
        table.AddColumn(new Column("co1") { NumberColumnsRepeated = (Limit - 1).ToString(System.Globalization.CultureInfo.InvariantCulture) });

        table.AddColumn(new Column("co1"));

        Assert.AreEqual(Limit, table.TotalNumberOfColumns());
    }

    [TestMethod]
    public void LimitIsStillEnforcedAfterColumnsGrewThroughTheListTest()
    {
        var table = new OpenDocumentTable("T");
        table.Columns.Add(new Column("co1") { NumberColumnsRepeated = Limit.ToString(System.Globalization.CultureInfo.InvariantCulture) });

        Assert.ThrowsExactly<InvalidOperationException>(() => table.AddColumn(new Column("co1")));
    }

    [TestMethod]
    public async Task CarriedTotalSurvivesSavingTest()
    {
        // Saving pads the last column out to the column limit and puts it back afterwards. The
        // carried total is derived from that same column, so it has to be right on the far side
        // of a save.
        var doc = new OpenDocumentSpreadsheet();
        var ag = new AutoGrid(doc, "T", 2, 3, "20mm");
        doc.Tables.Add(ag);

        using var mem = new MemoryStream();
        await doc.Save(mem, leaveOpen: true);

        Assert.AreEqual(3, ag.TotalNumberOfColumns(), "saving must not change the column count");

        ag.AddColumn(new Column("co1"));

        Assert.AreEqual(4, ag.TotalNumberOfColumns(), "a column added after a save still counts");
    }

    [TestMethod]
    public async Task LimitIsStillEnforcedAfterSavingTest()
    {
        var doc = new OpenDocumentSpreadsheet();
        var ag = new AutoGrid(doc, "T", 2, 3, "20mm");
        doc.Tables.Add(ag);

        // Added through AddColumn rather than by editing a column in place, so the carried total
        // sees it. Three columns plus one repeated (Limit - 4) times leaves room for exactly one
        // more.
        ag.AddColumn(new Column("co1") { NumberColumnsRepeated = (Limit - 4).ToString(System.Globalization.CultureInfo.InvariantCulture) });

        using var mem = new MemoryStream();
        await doc.Save(mem, leaveOpen: true);

        Assert.AreEqual(Limit - 1, ag.TotalNumberOfColumns(), "saving must not change the column count");

        ag.AddColumn(new Column("co1"));

        Assert.ThrowsExactly<InvalidOperationException>(() => ag.AddColumn(new Column("co1")));
    }

    [TestMethod]
    public void UnparseableRepeatCountIsTreatedAsZeroTest()
    {
        var table = new OpenDocumentTable("T");

        table.AddColumn(new Column("co1"));
        table.AddColumn(new Column("co1") { NumberColumnsRepeated = "not a number" });

        Assert.AreEqual(1, table.TotalNumberOfColumns());
    }

    [TestMethod]
    public void AddingNullColumnThrowsTest()
        => Assert.ThrowsExactly<ArgumentNullException>(() => new OpenDocumentTable("T").AddColumn((Column)null!));
}
