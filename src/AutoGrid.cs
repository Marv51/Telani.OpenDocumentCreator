using System.Diagnostics;
using System.Globalization;
using OpenDocumentCreator.Styles;

namespace OpenDocumentCreator;

/// <summary>
/// Adds to the functionality of OpenDocumentTable, enabling grid interface
/// </summary>
public class AutoGrid : OpenDocumentTable, IGridWriter
{
    private readonly OpenDocument doc;
    private readonly string defaultColWidth;

    /// <summary>
    /// The number of columns every row is known to hold cells for. Compared against
    /// <see cref="OpenDocumentTable.Columns"/> to decide whether the rows need padding, which keeps
    /// that walk off the per-cell write path. Comparing counts rather than remembering that this
    /// class grew the columns also covers columns added directly through
    /// <see cref="OpenDocumentTable.AddColumn(Column)"/> or the <see cref="OpenDocumentTable.Columns"/> list.
    /// </summary>
    private int paddedToColumnCount;

    /// <summary>
    /// Ensures that the table is large enough to have a row at the specified index, adding new empty rows if necessary.
    /// The added rows will have the same number of columns as the current table.
    /// In cases when a count or length is added to the index, the current index (here 'index') is ensured as well, so need to subtract 1 from the count/length to avoid adding an extra row/column.
    /// In other words either do the calculations entirely zero indexed or entirely one indexed (here it uses zero indexed) not mix them.
    /// The same goes for EnsureEnoughColumns
    /// </summary>
    /// <param name="index"></param>
    private void EnsureEnoughRows(int index)
    {
        var missing = index - Rows.Count + 1;
        if (missing <= 0)
        {
            return;
        }

        Rows.AddRange(Enumerable.Range(0, missing)
            .Select(_ =>
                       {
                           var row = new Row();
                           row.InsertCells(
                               Enumerable.Range(0, Columns.Count)
                                         .Select(_ => new OpenDocumentCell()));
                           return row;
                       }));
    }

    private void EnsureEnoughColumns(int index)
    {
        // Compare against Columns.Count, not Rows[y].Count: rows added by
        // EnsureEnoughRows start empty, so using Rows[y].Count would add
        // 'index' times new columns on every WriteRow past the initial row range —
        // exhausting the 16385-column limit on moderately sized exports.
        if (index >= Columns.Count)
        {
            AutoColumnProcessor.Apply(doc, this, AutoColumnProcessor.CreateColumns(index - Columns.Count + 1, defaultColWidth));
        }

        // A row can only fall behind when the column count grows: EnsureEnoughRows gives
        // every new row as many cells as there are columns at the time, and nothing else
        // shortens a row. So when the count has not moved since the last pass, there is
        // nothing to pad, and walking every row on every WriteCell made writing a cell
        // cost O(Rows.Count).
        if (paddedToColumnCount == Columns.Count)
        {
            return;
        }

        foreach (Row row in Rows)
        {
            while (Columns.Count > row.Count)
            {
                row.Add(new OpenDocumentCell());
            }
        }
        paddedToColumnCount = Columns.Count;
    }

    private void EnsureTableSize(int x, int y)
    {
        EnsureEnoughRows(y);
        EnsureEnoughColumns(x);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoGrid"/> class. Prepares a grid full of cells for the specified dimensions.
    /// </summary>
    /// <param name="doc">the OpenDocument used for style things.</param>
    /// <param name="name">the name of the created table</param>
    /// <param name="nRows">the height of the grid</param>
    /// <param name="nColumns">the width of the grid</param>
    /// <param name="columnWidth">the default width of any automatically created columns</param>
    public AutoGrid(OpenDocument doc, string name, int nRows, int nColumns, string columnWidth)
        : base(name)
    {
        this.doc = doc;
        defaultColWidth = columnWidth;
        AutoColumnProcessor.Apply(doc, this, AutoColumnProcessor.CreateColumns(nColumns, columnWidth));
        EnsureEnoughRows(nRows - 1);
    }

    /// <inheritdoc />
    public void WriteCell<T>(T content, OpenDocumentStyle? style = null, int x = 0, int y = 0) => WriteCell(x, y, content, style);

    /// <inheritdoc />
    public void WriteCell<T>(int x, int y, T content, OpenDocumentStyle? style = null)
    {
        if (x < 0 || y < 0)
        {
            throw new ArgumentException("Invalid Arguments: the indexes can't be less than 0 or exceed table dimensions");
        }
        EnsureTableSize(x, y);

        try
        {
            var target_cell = Rows[y][x];

            if (content is OpenDocumentCell cell)
            {
                if (style is not null)
                {
                    cell.Style = style;
                }
                Rows[y].Replace(x, cell);
            }
            else
            {
                switch (content)
                {
                    case string strVal:
                        target_cell.Content = strVal;
                        break;
                    case double dblVal:
                        target_cell.FloatContent = Convert.ToSingle(dblVal);
                        break;
                    case Uri uriVal:
                        target_cell.Link = uriVal;
                        break;
                    case OpenDocumentFrame frame:
                        target_cell.Frame = frame;
                        break;
                    case int repeatedColumns:
                        target_cell.NumberColumnsRepeated = repeatedColumns;
                        break;
                    case float floatVal:
                        target_cell.FloatContent = floatVal;
                        break;
                    default:
                        Debug.Fail("This can't be good.");

                        // New properties need to be copied here
                        target_cell.Content = string.Empty;
                        break;
                }
                if (style is not null)
                {
                    target_cell.Style = style;
                }
            }
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    /// <inheritdoc />
    public void SetCellSpan(int x, int y, int rowSpan, int columnSpan)
    {
        // Span is not zero indexed
        if (y < 0 || x < 0 || rowSpan <= 0 || columnSpan <= 0 || x + columnSpan > Columns.Count)
        {
            throw new ArgumentException("Invalid Arguments: the indexes (" + x + "," + y + ") and spans (" + columnSpan + ", " + rowSpan + ") should be greater than 0 and 1 respectively and should not exceed table " + Name + " dimensions (" + Columns.Count + "," + Rows.Count + ")");
        }
        EnsureEnoughRows(y + rowSpan - 1);

        OpenDocumentCell cell = Rows[y][x];

        if (cell.IsCovered)
        {
            throw new InvalidOperationException("The cell is already covered (" + x + ", " + y + ")");
        }

        // Change the cells spanned previously by the cell to uncovered.
        for (int c = 0; c < cell.ColumnsSpanned; c++)
        {
            for (int r = 0; r < cell.RowsSpanned; r++)
            {
                Rows[y + r][x + c].IsCovered = false;
            }
        }

        // Try to set the cells currently spanned by the cell as covered, raise exception when overlap with a cell spanning multiple cells(span >1) or cell covered by another cell
        for (int c = 0; c < columnSpan; c++)
        {
            for (int r = 0; r < rowSpan; r++)
            {
                var element = Rows[y + r][x + c];
                if (element.IsCovered || element.RowsSpanned > 1 || element.ColumnsSpanned > 1)
                {
                    throw new InvalidOperationException("The cell (" + (x + c) + "," + (y + r) + ") is already covered for table " + Name);
                }
                else if (r == 0 && c == 0)
                {
                    continue;
                }
                element.IsCovered = true;
            }
        }
        cell.RowsSpanned = rowSpan;
        cell.ColumnsSpanned = columnSpan;
    }

    /// <summary>
    /// Way to change the Column widths after they are initialized
    /// </summary>
    /// <param name="x">the index of the (first) column</param>
    /// <param name="widths">the width or widths to set. this needs to be a
    /// string representing a number of millimeters plus the ending "mm". (like 1.2mm, or 2mm)</param>
    public void SetColumnsWidth(int x, params string[] widths)
    {
        EnsureEnoughColumns(x + widths.Length - 1);
        foreach (string width in widths)
        {
            if (x < 0 || x >= Columns.Count)
            {
                throw new ArgumentException("Invalid Column index " + x + " for table " + Name + " with " + Columns.Count + " columns");
            }

            var properties = new TableColumnProperties
            {
                BreakBefore = BreakValue.Auto,
                ColumnWidth = new Measurement(decimal.Parse(width.AsSpan(0, width.Length - 2), CultureInfo.InvariantCulture), Unit.MM),
            };

            Columns[x].StyleName = this.doc.GetOrAddTableColumnStyle(properties).Name;
            x++;
        }
    }

    /// <summary>
    /// Way to change the Column styles to a predefined style after they are initialized
    /// </summary>
    /// <param name="x">the index of the column</param>
    /// <param name="styles">the name or names of the styles to set. These need to exist in the
    /// document and, if they declare a family, be <see cref="StyleFamily.TableColumn"/> styles.</param>
    /// <exception cref="ArgumentException">if the column index is outside of the acceptable range</exception>
    /// <exception cref="InvalidOperationException">if a style is not found or is not a column style</exception>
    public void SetColumnsStyle(int x, params string[] styles)
    {
        foreach (string style in styles)
        {
            RequireColumnIndex(x);
            RequireStyle(style, StyleFamily.TableColumn);
            Columns[x].StyleName = style;
            x++;
        }
    }

    /// <summary>
    /// Throws unless the index addresses a column that exists on this table.
    /// </summary>
    /// <param name="x">the index of the column</param>
    /// <exception cref="ArgumentException">if the index is outside of the acceptable range</exception>
    private void RequireColumnIndex(int x)
    {
        if (x < 0 || x >= Columns.Count)
        {
            throw new ArgumentException("Invalid Column index " + x + " for table " + Name + " with " + Columns.Count + " columns");
        }
    }

    /// <summary>
    /// Looks up a style by name and checks that it is usable in the given role.
    ///
    /// <see cref="OpenDocumentStyle.Family"/> is optional, so a style that declares no
    /// family at all is accepted for any role; only a style that declares a different
    /// family is rejected.
    /// </summary>
    /// <param name="styleName">the name of the style to look up</param>
    /// <param name="family">the family the style has to belong to, if it declares one</param>
    /// <exception cref="InvalidOperationException">if the style does not exist or belongs to another family</exception>
    private void RequireStyle(string styleName, StyleFamily family)
    {
        var foundStyle = doc.Styles.Values.FirstOrDefault(s => s.Name == styleName)
            ?? throw new InvalidOperationException("The required style " + styleName + " was not found for table " + Name);

        if (foundStyle.Family is not null && foundStyle.Family != family)
        {
            throw new InvalidOperationException("The style " + styleName + " is a " + foundStyle.Family + " style, but a " + family + " style is required here");
        }
    }

    /// <summary>
    /// Way to set the default cell style of one or more columns after they are initialized.
    ///
    /// Cells in that column that carry no style of their own are rendered with this style,
    /// which is the ODF-native way to style the body of a table: one attribute per column
    /// instead of a style on every cell. A style set on the row wins over this one,
    /// as does a style set on the cell itself.
    /// </summary>
    /// <param name="x">the index of the (first) column</param>
    /// <param name="styles">the name or names of the cell styles to set. These need to exist
    /// in the document and, if they declare a family, be <see cref="StyleFamily.TableCell"/> styles.</param>
    /// <exception cref="ArgumentException">if the column index is outside of the acceptable range</exception>
    /// <exception cref="InvalidOperationException">if a style is not found or is not a cell style</exception>
    public void SetColumnsDefaultCellStyle(int x, params string[] styles)
    {
        EnsureEnoughColumns(x + styles.Length - 1);
        foreach (string style in styles)
        {
            RequireColumnIndex(x);
            RequireStyle(style, StyleFamily.TableCell);
            Columns[x].DefaultCellStyleName = style;
            x++;
        }
    }

    /// <inheritdoc />
    public void WriteRow(int x, int y, Row row) => WriteRows(x, y, row);

    /// <inheritdoc />
    public int WriteRows(int x, int y, IEnumerable<Row> rows)
    {
        // Counting and then iterating walked the sequence twice. For a lazily produced source
        // that re-runs the whole query, and the rows written are then not the rows counted.
        var materialized = rows as IReadOnlyList<Row> ?? [.. rows];
        var length = materialized.Count;
        EnsureEnoughRows(y + length - 1);

        foreach (Row row in materialized)
        {
            EnsureEnoughColumns(x + row.Count - 1);
            WriteRowInternal(x, y, row);
            y++;
        }
        return length;
    }

    /// <inheritdoc />
    public int WriteRows(int x, int y, params Row[] rows)
        => WriteRows(x, y, (IEnumerable<Row>)rows);

    private void WriteRowInternal(int x, int y, Row row)
    {
        if (x < 0 || x >= Columns.Count || y < 0)
        {
            throw new ArgumentException("Invalid Arguments: the indexes (" + x + "," + y + ") should be greater than 0 and should not exceed table " + Name + " dimensions (" + Columns.Count + "," + Rows.Count + ")");
        }
        Rows[y].Style = row.Style;

        for (int i = 0; i < row.Count; i++)
        {
            OpenDocumentCell cell = row[i];
            var wrote = false;
            if (cell.Content is not null)
            {
                WriteCell(i + x, y, cell.Content, cell.Style);
                wrote = true;
            }
            if (cell.FloatContent is not null)
            {
                WriteCell(i + x, y, cell.FloatContent, cell.Style);
                wrote = true;
            }
            if (cell.Link is not null)
            {
                WriteCell(i + x, y, cell.Link, cell.Style);
                wrote = true;
            }
            if (cell.Frame is not null)
            {
                WriteCell(i + x, y, cell.Frame, cell.Style);
                wrote = true;
            }
            if (cell.NumberColumnsRepeated != 1)
            {
                WriteCell(i + x, y, cell.NumberColumnsRepeated, cell.Style);
                wrote = true;
            }
            if (cell.ColumnsSpanned > 1 || cell.RowsSpanned > 1)
            {
                SetCellSpan(i + x, y, cell.RowsSpanned, cell.ColumnsSpanned);
                wrote = true;
                i += cell.ColumnsSpanned - 1;
            }
            if (!wrote)
            {
                Rows[y][i + x].Style = cell.Style;
            }
        }
    }

    /// <summary>
    /// Sets the style for a specific row.
    /// </summary>
    /// <param name="y">index of row</param>
    /// <param name="style">the style to apply</param>
    /// <exception cref="ArgumentException">if index outside of acceptable range</exception>
    public void WriteRowStyle(int y, OpenDocumentStyle style)
    {
        if (y < 0)
        {
            throw new ArgumentException("Invalid Row index");
        }
        EnsureEnoughRows(y);
        Rows[y].Style = style;
    }

    /// <inheritdoc />
    public void WriteColumn<T>(int x, int y, IEnumerable<T> content, OpenDocumentStyle? style = null)
    {
        if (x < 0 || x >= Columns.Count || y < 0)
        {
            throw new ArgumentException("Invalid Arguments: The indexes(" + x + ", " + y + ") should be greater than 0 and should not exceed table " + Name + " dimensions(" + Columns.Count + ", " + Rows.Count + ")");
        }
        var values = content as IReadOnlyList<T> ?? content.ToList();
        EnsureEnoughRows(values.Count - 1 + y);

        for (int i = 0; i < values.Count; i++)
        {
            WriteCell(x, i + y, values[i], style);
        }
    }

    /// <inheritdoc />
    public int WriteColumns<T>(int x, int y, IEnumerable<IEnumerable<T>> contents, OpenDocumentStyle? style = null)
    {
        var count = 0;
        foreach (var c in contents)
        {
            WriteColumn(x, y, c, style);
            x++;
            count++;
        }
        return count;
    }
}
