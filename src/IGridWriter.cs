using System.Collections.ObjectModel;
using OpenDocumentCreator.Styles;

namespace OpenDocumentCreator;

/// <summary>
/// Interface to write cells to a grid.
/// </summary>
public interface IGridWriter
{
    /// <summary>
    /// Sets the span of the cell given the coordinates, sets cells in its span to covered, raises exception when spans overlap
    /// </summary>
    /// <param name="x">the x coordinate to write to</param>
    /// <param name="y">the y coordinate to write to</param>
    /// <param name="rowSpan">how many rows the cell should span</param>
    /// <param name="columnSpan">how many columns this cell should span</param>
    void SetCellSpan(int x, int y, int rowSpan, int columnSpan);

    /// <summary>
    /// Writes the content to the grid cell at the given coordinates
    /// </summary>
    /// <param name="x">the x coordinate to write to</param>
    /// <param name="y">the y coordinate to write to</param>
    /// <param name="content">The content to write into that cell</param>
    /// <param name="style">the style that should be applied to this cell</param>
    /// <typeparam name="T">The type of content to write.</typeparam>
    void WriteCell<T>(int x, int y, T content, OpenDocumentStyle? style = null);

    /// <summary>
    /// Writes the content to the grid cell given the coordinates, with optional coordinates
    ///
    /// This overload enables only specifying x or y.
    /// </summary>
    /// <param name="content">The content to write into that cell</param>
    /// <param name="style">the style that should be applied to this cell</param>
    /// <param name="x">the x coordinate to write to (defaults to 0)</param>
    /// <param name="y">the y coordinate to write to (defaults to 0)</param>
    /// <typeparam name="T">The type of content to write.</typeparam>
    void WriteCell<T>(T content, OpenDocumentStyle? style = null, int x = 0, int y = 0);

    /// <summary>
    /// Writes a row to the grid, given the coordinates
    /// </summary>
    /// <param name="x">the x coordinate of the first item</param>
    /// <param name="y">the y coordinate of all the items</param>
    /// <param name="row">the row to add</param>
    void WriteRow(int x, int y, Row row);

    /// <summary>
    /// Writes multiple rows to the grid, given the coordinates
    /// </summary>
    /// <param name="x">the x coordinate of the first item in each added row</param>
    /// <param name="y">the y coordinate of the first row to be added</param>
    /// <param name="rows">the row or rows to add to the grid.</param>
    /// <returns>The number of rows written</returns>
    int WriteRows(int x, int y, IEnumerable<Row> rows);

    /// <summary>
    /// Writes multiple rows to the grid, given the coordinates
    /// </summary>
    /// <param name="x">the x coordinate of the first item in each added row</param>
    /// <param name="y">the y coordinate of the first row to be added</param>
    /// <param name="rows">the row or rows to add to the grid.</param>
    /// <returns>The number of rows written</returns>
    int WriteRows(int x, int y, params Row[] rows);

    /// <summary>
    /// Sets the style for a specific row.
    /// </summary>
    /// <param name="y">index of row</param>
    /// <param name="style">style</param>
    /// <exception cref="ArgumentException">if index outside of acceptable range</exception>
    void WriteRowStyle(int y, OpenDocumentStyle style);

    /// <summary>
    /// Writes a column to the grid, given the coordinates
    /// </summary>
    /// <param name="x">the x coordinate of the first item</param>
    /// <param name="y">the y coordinate of all the items</param>
    /// <param name="content">the content to add</param>
    /// <param name="style">the style that should be applied to every written cell</param>
    /// <typeparam name="T">The type of content to write.</typeparam>
    void WriteColumn<T>(int x, int y, IEnumerable<T> content, OpenDocumentStyle? style = null);

    /// <summary>
    /// Writes multiple columns to the grid. Different lengths are supported.
    /// </summary>
    /// <param name="x">the x-coordinate of the first item</param>
    /// <param name="y">the y-coordinate of the first item</param>
    /// <param name="contents">A collection of collections of items to write</param>
    /// <param name="style">the style that should be applied to every written cell</param>
    /// <typeparam name="T">The type of content to write.</typeparam>
    /// <returns>Number of written columns</returns>
    int WriteColumns<T>(int x, int y, IEnumerable<IEnumerable<T>> contents, OpenDocumentStyle? style = null);
}