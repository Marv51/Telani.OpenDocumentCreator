using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using OpenDocumentCreator.Styles;

namespace OpenDocumentCreator;

/// <summary>
/// Represents a row in a ODS file. When finalizing the document this class gets converted into an OpenDocumentRow.
///
///
/// This class behaves like a collection to enable nice syntax shortcuts.
/// </summary>
#pragma warning disable CA1710 // Identifiers should have correct suffix
public class Row : IList<OpenDocumentCell>
#pragma warning restore CA1710 // Identifiers should have correct suffix
{
    /// <summary>
    /// The row style to use for this row.
    /// </summary>
    public OpenDocumentStyle? Style { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Row"/> class with applied row style.
    /// </summary>
    /// <param name="style">the style to apply to this row.</param>
    public Row(OpenDocumentStyle style)
        : this() => Style = style;

    /// <summary>
    /// Initializes a new instance of the <see cref="Row"/> class.
    /// </summary>
    public Row() => _readOnlyCells = new ReadOnlyCollection<OpenDocumentCell>(_cells);

    /// <summary>
    /// Initializes a new instance of the <see cref="Row"/> class from this enumeration of cells.
    /// </summary>
    /// <param name="cells">Cells to include in the row</param>
    /// <exception cref="System.ArgumentNullException">if the <paramref name="cells"/> argument is null</exception>
    public Row(IEnumerable<OpenDocumentCell> cells)
        : this()
    {
        ArgumentNullException.ThrowIfNull(cells, nameof(cells));

        foreach (var cell in cells)
        {
            InsertCell(cell);
        }
    }

    /// <summary>
    /// The collection of cells in this row.
    /// </summary>
    public IReadOnlyCollection<OpenDocumentCell> Cells => _readOnlyCells;

    /// <summary>
    /// Gets or sets the cell at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">the index of the cell</param>
    /// <returns>the cell at that index</returns>
    /// <exception cref="ArgumentOutOfRangeException">if the index is outside of the row</exception>
    public OpenDocumentCell this[int index]
    {
        get => _cells[index];
        set => _cells[index] = value;
    }

    /// <summary>
    /// Gets the number of cells in this row.
    /// </summary>
    public int Count => _cells.Count;

    /// <summary>
    /// Is this collection readonly? Always returns false.
    /// </summary>
    public bool IsReadOnly => false;

    private readonly List<OpenDocumentCell> _cells = [];

    // Wrapping the list once keeps Cells allocation free; the wrapper stays in
    // sync with the list it wraps.
    private readonly ReadOnlyCollection<OpenDocumentCell> _readOnlyCells;

    private static readonly char[] Separator = ['|'];

    /// <summary>
    /// Insert a new cell with text <paramref name="cell"/>.
    /// </summary>
    /// <param name="cell"></param>
    public void InsertCell(string? cell)
    {
        InsertCell(new OpenDocumentCell(cell));
    }

    /// <summary>
    /// Insert a new cell with <paramref name="text"/> and applied cell <paramref name="style"/>.
    /// </summary>
    /// <param name="text">Text of the cell</param>
    /// <param name="style">Style of the cell</param>
    public void InsertCell(string? text, OpenDocumentStyle style)
    {
        InsertCell(new OpenDocumentCell(text, style));
    }

    /// <summary>
    /// Add a cell.
    /// </summary>
    /// <param name="cell">The cell to add.</param>
    /// <exception cref="System.ArgumentNullException">if the <paramref name="cell"/> is null</exception>
    public void InsertCell(OpenDocumentCell cell)
    {
        ArgumentNullException.ThrowIfNull(cell, nameof(cell));

        _cells.Add(cell);
        if (cell.ColumnsSpanned > 1)
        {
            for (var i = 0; i < cell.ColumnsSpanned - 1; i++)
            {
                _cells.Add(new OpenDocumentCell() { IsCovered = true });
            }
        }
    }

    /// <summary>
    /// This is an alias for the more descriptive InsertCell so that collection initialization works.
    /// </summary>
    /// <param name="item"></param>
    public void Add(OpenDocumentCell item)
    {
        InsertCell(item);
    }

    /// <summary>
    /// This is an alias for the more descriptive InsertCell so that collection initialization works.
    /// </summary>
    /// <param name="cell"></param>
    public void Add(string? cell)
    {
        InsertCell(new OpenDocumentCell(cell));
    }

    /// <summary>
    /// Add a tuple of text and style as a new cell.
    /// </summary>
    /// <param name="cell"></param>
    public void Add((string Text, OpenDocumentStyle Style) cell)
    {
        InsertCell(cell.Text, cell.Style);
    }

    /// <inheritdoc/>
    public IEnumerator<OpenDocumentCell> GetEnumerator()
    {
        return _cells.GetEnumerator();
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return (IEnumerator)GetEnumerator();
    }

    /// <summary>
    /// Inserts an enumeration of cells at the end of the row.
    /// </summary>
    /// <param name="cells"></param>
    /// <exception cref="System.ArgumentNullException">If <paramref name="cells"/> is null.</exception>
    public void InsertCells(IEnumerable<OpenDocumentCell> cells)
    {
        ArgumentNullException.ThrowIfNull(cells, nameof(cells));

        foreach (var c in cells)
        {
            InsertCell(c);
        }
    }

    /// <summary>
    ///
    /// Insert a covered cell with a single *
    ///
    /// "| {0} | {1} | {2} | {3} |"
    /// </summary>
    /// <param name="templateString"></param>
    /// <param name="list"></param>
    public void InsertCellsFromTemplateString(string templateString, params object?[] list)
    {
        InsertCellsFromTemplateString(null, templateString, list);
    }

    /// <summary>
    /// Inserts cells from a template string.
    /// This methods uses <paramref name="style"/> for all cells created from the template string.
    ///
    /// The template string can look like this:
    /// "| {0} | {1} | {2} | {3} |"
    ///
    /// Insert a covered cell with a single *
    /// </summary>
    /// <param name="style"></param>
    /// <param name="templateString"></param>
    /// <param name="list"></param>
    /// <exception cref="System.ArgumentNullException">If <paramref name="templateString"/> is null.</exception>
    public void InsertCellsFromTemplateString(OpenDocumentStyle? style, string templateString, params object?[] list)
    {
        ArgumentNullException.ThrowIfNull(templateString, nameof(templateString));

        var parts = templateString.Trim().Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            if (p.Trim() == "*")
            {
                InsertCell(new OpenDocumentCell() { IsCovered = true, Style = style });
            }
            else
            {
                InsertCell(new OpenDocumentCell(string.Format(CultureInfo.InvariantCulture, p.Trim(), list))
                {
                    Style = style,
                });
            }
        }
    }

    /// <summary>
    /// Removes all Cells from this Row.
    /// </summary>
    public void Clear() => _cells.Clear();

    /// <summary>
    /// Does this row contain the <paramref name="item"/> cell?
    /// </summary>
    /// <param name="item">a cell to look for.</param>
    /// <returns>true if row contains the cell</returns>
    public bool Contains(OpenDocumentCell item) => _cells.Contains(item);

    /// <summary>
    /// Copy the cells into an array.
    /// </summary>
    /// <param name="array">the target array</param>
    /// <param name="arrayIndex">the index at which to start inserting into the target array</param>
    public void CopyTo(OpenDocumentCell[] array, int arrayIndex) => _cells.CopyTo(array, arrayIndex);

    /// <summary>
    /// Remove the <paramref name="item"/> from this row.
    /// </summary>
    /// <param name="item">the item to remove</param>
    /// <returns>true if the item was successfully removed, otherwise false</returns>
    public bool Remove(OpenDocumentCell item) => _cells.Remove(item);

    /// <summary>
    /// Replace a cell at a specified index of the row.
    /// </summary>
    /// <param name="x">the index</param>
    /// <param name="cell">the new cell</param>
    public void Replace(int x, OpenDocumentCell cell) => _cells[x] = cell;

    /// <summary>
    /// Determines the index of <paramref name="item"/> in this row.
    /// </summary>
    /// <param name="item">the cell to look for</param>
    /// <returns>the index of the cell, or -1 if it is not in this row</returns>
    public int IndexOf(OpenDocumentCell item) => _cells.IndexOf(item);

    /// <summary>
    /// Inserts a cell at <paramref name="index"/>.
    ///
    /// Unlike <see cref="InsertCell(OpenDocumentCell)"/> this does not append covered cells for a
    /// cell that spans several columns, because that would shift the cells after it out of place.
    /// </summary>
    /// <param name="index">the index to insert at</param>
    /// <param name="item">the cell to insert</param>
    /// <exception cref="ArgumentOutOfRangeException">if the index is outside of the row</exception>
    public void Insert(int index, OpenDocumentCell item) => _cells.Insert(index, item);

    /// <summary>
    /// Removes the cell at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">the index of the cell to remove</param>
    /// <exception cref="ArgumentOutOfRangeException">if the index is outside of the row</exception>
    public void RemoveAt(int index) => _cells.RemoveAt(index);
}
