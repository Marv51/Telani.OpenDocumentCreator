namespace OpenDocumentCreator.Tests;

[TestClass]
public sealed class AutoGridTests
{
    private readonly OpenDocumentSpreadsheet doc = new();

    private static readonly string[] LongColumn = ["a", "b", "c"];
    private static readonly string[] ShortColumn = ["d", "e"];

    [TestMethod]
    public async Task SetColumnsWidthTest()
    {
        var ag = SetupAutoGrid(20, 10);

        ag.SetColumnsWidth(x: 1, "17mm", "22mm", "22mm");

        var styles = doc.Styles.Where(a => a.Value.Family == DataTypes.StyleFamily.TableColumn).ToList();
        Assert.HasCount(3, styles);

        AssertRectangleGrid(ag);
        await SaveDoc();
    }

    [TestMethod]
    public async Task SetColumnsStylesFailTest()
    {
        var ag = SetupAutoGrid();
        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            ag.SetColumnsStyle(x: 1, "telaniTEST");
        });

        doc.Styles.Add("telaniTEST", new Styles.OpenDocumentStyle
        {
            Name = "telaniTEST"
        });

        ag.SetColumnsStyle(x: 1, "telaniTEST");

        Assert.AreEqual("telaniTEST", ag.Columns[1].StyleName);

        AssertRectangleGrid(ag);
        await SaveDoc();
    }

    [TestMethod]
    public async Task LongRowTest()
    {
        var ag = SetupAutoGrid();

        ag.WriteCell(10, 2, "Testing");

        Assert.AreEqual("Test-Table", doc.Tables.First().Name);

        var row = doc.Tables.First().Rows[2];
        Assert.AreEqual("Testing", row.ElementAt(10).Content);

        AssertRectangleGrid(ag);
        await SaveDoc();
    }

    private AutoGrid SetupAutoGrid(int x = 5, int y = 5)
    {
        var ag = new AutoGrid(doc, "Test-Table", x, y, "20mm");
        doc.Tables.Add(ag);
        return ag;
    }

    [TestMethod]
    public async Task LongColumnsTest()
    {
        var ag = SetupAutoGrid();

        ag.WriteCell(2, 10, "Testing");

        Assert.AreEqual("Test-Table", doc.Tables.First().Name);

        var row = doc.Tables.First().Rows[10];
        Assert.AreEqual("Testing", row.ElementAt(2).Content);
        AssertRectangleGrid(ag);

        await SaveDoc();
    }

    [TestMethod]
    public async Task SetColumnWithOutsideTest()
    {
        var ag = SetupAutoGrid();

        ag.SetColumnsWidth(10, "50mm");

        Assert.AreEqual("Test-Table", doc.Tables.First().Name);
        AssertRectangleGrid(ag);
        await SaveDoc();
    }

    [TestMethod]
    public async Task WriteColumnAppliesStyleToEveryCellTest()
    {
        var ag = SetupAutoGrid();
        var style = new Styles.OpenDocumentStyle { Name = "ce_border" };

        ag.WriteColumn(1, 2, LongColumn, style);

        for (int i = 0; i < LongColumn.Length; i++)
        {
            var cell = ag.Rows[2 + i].ElementAt(1);
            Assert.AreEqual(LongColumn[i], cell.Content);
            Assert.AreSame(style, cell.Style);
        }

        // cells outside the written range keep their default style
        Assert.IsNull(ag.Rows[1].ElementAt(1).Style);

        AssertRectangleGrid(ag);
        await SaveDoc();
    }

    [TestMethod]
    public async Task WriteColumnWithoutStyleLeavesCellsUnstyledTest()
    {
        var ag = SetupAutoGrid();

        ag.WriteColumn(1, 0, ShortColumn);

        Assert.IsNull(ag.Rows[0].ElementAt(1).Style);
        Assert.IsNull(ag.Rows[1].ElementAt(1).Style);

        AssertRectangleGrid(ag);
        await SaveDoc();
    }

    [TestMethod]
    public async Task WriteColumnsAppliesStyleToEveryCellTest()
    {
        var ag = SetupAutoGrid();
        var style = new Styles.OpenDocumentStyle { Name = "ce_border" };

        var written = ag.WriteColumns(1, 1, [LongColumn, ShortColumn], style);

        Assert.AreEqual(2, written);
        Assert.AreSame(style, ag.Rows[3].ElementAt(1).Style);
        Assert.AreSame(style, ag.Rows[2].ElementAt(2).Style);
        Assert.AreEqual("e", ag.Rows[2].ElementAt(2).Content);

        AssertRectangleGrid(ag);
        await SaveDoc();
    }

    [TestMethod]
    public async Task WriteColumnStylesContentWithoutCellWrapperTest()
    {
        // OpenDocumentFrame cannot be wrapped in an OpenDocumentCell by the caller,
        // so the style overload is the only way to give those cells a border.
        var ag = SetupAutoGrid();
        var style = new Styles.OpenDocumentStyle { Name = "ce_border" };

        ag.WriteColumn(0, 0, [new OpenDocumentFrame(), new OpenDocumentFrame()], style);

        Assert.AreSame(style, ag.Rows[0].ElementAt(0).Style);
        Assert.AreSame(style, ag.Rows[1].ElementAt(0).Style);
        Assert.IsNotNull(ag.Rows[1].ElementAt(0).Frame);

        AssertRectangleGrid(ag);
        await SaveDoc();
    }

    [TestMethod]
    public void SetColumnsStyleRejectsWrongFamilyTest()
    {
        var ag = SetupAutoGrid();
        doc.Styles.Add("ce_wrong_family", new Styles.OpenDocumentStyle
        {
            Name = "ce_wrong_family",
            Family = DataTypes.StyleFamily.TableCell,
        });

        var before = ag.Columns[1].StyleName;

        Assert.ThrowsExactly<InvalidOperationException>(() => ag.SetColumnsStyle(1, "ce_wrong_family"));

        Assert.AreEqual(before, ag.Columns[1].StyleName, "a rejected style must not be applied");
    }

    [TestMethod]
    public async Task SetColumnsDefaultCellStyleTest()
    {
        var ag = SetupAutoGrid();
        doc.Styles.Add("ce_border", new Styles.OpenDocumentStyle
        {
            Name = "ce_border",
            Family = DataTypes.StyleFamily.TableCell,
        });

        ag.SetColumnsDefaultCellStyle(1, "ce_border", "ce_border");

        Assert.AreEqual("ce_border", ag.Columns[1].DefaultCellStyleName);
        Assert.AreEqual("ce_border", ag.Columns[2].DefaultCellStyleName);
        Assert.AreEqual("ce1", ag.Columns[0].DefaultCellStyleName, "untouched columns keep the default");

        Assert.Contains("table:default-cell-style-name=\"ce_border\"", await SaveDocAndReadContentXml());

        AssertRectangleGrid(ag);
    }

    [TestMethod]
    public void SetColumnsDefaultCellStyleGrowsTableTest()
    {
        var ag = SetupAutoGrid();
        doc.Styles.Add("ce_border", new Styles.OpenDocumentStyle
        {
            Name = "ce_border",
            Family = DataTypes.StyleFamily.TableCell,
        });

        ag.SetColumnsDefaultCellStyle(10, "ce_border");

        Assert.AreEqual("ce_border", ag.Columns[10].DefaultCellStyleName);
        AssertRectangleGrid(ag);
    }

    [TestMethod]
    public void SetColumnsDefaultCellStyleFailTest()
    {
        var ag = SetupAutoGrid();

        Assert.ThrowsExactly<InvalidOperationException>(() => ag.SetColumnsDefaultCellStyle(1, "missing"));

        doc.Styles.Add("co_wrong_family", new Styles.OpenDocumentStyle
        {
            Name = "co_wrong_family",
            Family = DataTypes.StyleFamily.TableColumn,
        });

        Assert.ThrowsExactly<InvalidOperationException>(() => ag.SetColumnsDefaultCellStyle(1, "co_wrong_family"));

        Assert.AreEqual("ce1", ag.Columns[1].DefaultCellStyleName);
    }

    [TestMethod]
    public void SetColumnsDefaultCellStyleAcceptsStyleWithoutFamilyTest()
    {
        var ag = SetupAutoGrid();
        doc.Styles.Add("ce_no_family", new Styles.OpenDocumentStyle { Name = "ce_no_family" });

        ag.SetColumnsDefaultCellStyle(1, "ce_no_family");

        Assert.AreEqual("ce_no_family", ag.Columns[1].DefaultCellStyleName);
    }

    private async Task<string> SaveDocAndReadContentXml()
    {
        MemoryStream mem = new();

        // Save closes the stream, so read the bytes back out of the closed stream.
        await doc.Save(mem);
        using var saved = new MemoryStream(mem.ToArray());
        using var zip = new System.IO.Compression.ZipArchive(saved, System.IO.Compression.ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("content.xml")!.Open());
        return await reader.ReadToEndAsync();
    }

    private async Task SaveDoc()
    {
        MemoryStream mem = new();
        await doc.Save(mem);
    }

    private static void AssertRectangleGrid(AutoGrid ag)
    {
        var rows = ag.Rows.Count;
        var firstRow = ag.Rows[0].Count;

        foreach (var r in ag.Rows)
        {
            Assert.HasCount(firstRow, r);
        }
    }

    [TestMethod]
    public async Task WriteRowOutsideTest()
    {
        var ag = SetupAutoGrid();

        ag.WriteRows(10, 2, new Row(
            [new OpenDocumentCell("1"), new OpenDocumentCell("2")]
            ));

        Assert.HasCount(5, ag.Rows);
        Assert.HasCount(12, ag.Rows[0]);
        AssertRectangleGrid(ag);

        Assert.AreEqual("Test-Table", doc.Tables.First().Name);
        

        await SaveDoc();
    }

    [TestMethod]
    public async Task WriteLongRowOutsideTest()
    {
        var ag = SetupAutoGrid();

        ag.WriteRows(2, 2, new Row(
            [   new OpenDocumentCell("1"), 
                new OpenDocumentCell("2"),
                new OpenDocumentCell("3"),
                new OpenDocumentCell("4"),
                new OpenDocumentCell("5"),
                new OpenDocumentCell("6"),
                new OpenDocumentCell("7"),
            ]
            ));

        Assert.AreEqual("Test-Table", doc.Tables.First().Name);
        AssertRectangleGrid(ag);

        await SaveDoc();
    }

    [TestMethod]
    public async Task WriteRowsTest()
    {
        var ag = SetupAutoGrid();

        ag.WriteRows(2, 2,
            new Row([new OpenDocumentCell("1")]),
            new Row([new OpenDocumentCell("2")])
            );

        Assert.AreEqual("Test-Table", doc.Tables.First().Name);
        AssertRectangleGrid(ag);

        await SaveDoc();
    }

    [TestMethod]
    public void WritingRowsPastInitialBoundsDoesNotInflateColumnCount()
    {
        // Reproduces a bug where writing rows past the initial row count
        // caused the column count to grow by row.Count on every WriteRow call,
        // eventually exceeding Excel's 16385-column limit for moderately sized
        // exports. See OpenDocumentTable.AddColumn.
        var ag = SetupAutoGrid(x: 5, y: 10);

        for (int y = 5; y < 50; y++)
        {
            ag.WriteRow(0, y, new Row([
                new OpenDocumentCell("a"),
                new OpenDocumentCell("b"),
                new OpenDocumentCell("c"),
            ]));
        }

        Assert.HasCount(10, ag.Columns, "column count must not grow when rows that fit within existing columns are written past the initial row range");
        AssertRectangleGrid(ag);
    }

    [TestMethod] 
    public void EnsuringRowsTest() 
    { 
        var ag = SetupAutoGrid(x: 5, y: 5);
        Assert.HasCount(5, ag.Rows);
        ag.WriteRow(0, 10, []);
        Assert.HasCount(11, ag.Rows);
        ag.WriteRow(0, 10, []);
        Assert.HasCount(11, ag.Rows, "row count must only grow to the required size once");
        ag.WriteRow(0, 15, []);
        Assert.HasCount(16, ag.Rows);
        AssertRectangleGrid(ag); 
    }
}