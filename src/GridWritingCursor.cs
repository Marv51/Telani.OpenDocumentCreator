using OpenDocumentCreator.Styles;

namespace OpenDocumentCreator;

/// <summary>
/// Works together with AutoGrid enables the passing of a dedicated writer to a function.
/// Moves the AutoGrid coordinate System by the specified offset.
///
/// </summary>
public class GridWritingCursor : IGridWriter
{
    private readonly IGridWriter writer;

    /// <summary>
    /// X offset to translate the inner writer by.
    /// </summary>
    public int XOffset { get; set; }

    /// <summary>
    /// Y offset to translate the inner writer by.
    /// </summary>
    public int YOffset { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GridWritingCursor"/> class.
    /// </summary>
    /// <param name="writer">The inner writer</param>
    /// <param name="xOffset">X offset to translate the inner writer by.</param>
    /// <param name="yOffset">Y offset to translate the inner writer by.</param>
    public GridWritingCursor(IGridWriter writer, int xOffset = 0, int yOffset = 0)
    {
        this.writer = writer;
        XOffset = xOffset;
        YOffset = yOffset;
    }

    /// <inheritdoc />
    public void SetCellSpan(int x, int y, int rowSpan, int columnSpan)
        => writer.SetCellSpan(x + XOffset, y + YOffset, rowSpan, columnSpan);

    /// <inheritdoc />
    public void WriteCell<T>(int x, int y, T content, OpenDocumentStyle? style = null)
        => writer.WriteCell(x + XOffset, y + YOffset, content, style);

    /// <inheritdoc />
    public void WriteRow(int x, int y, Row row)
        => writer.WriteRow(x + XOffset, y + YOffset, row);

    /// <inheritdoc />
    public int WriteRows(int x, int y, IEnumerable<Row> rows)
        => writer.WriteRows(x + XOffset, y + YOffset, rows);

    /// <inheritdoc />
    public int WriteRows(int x, int y, params Row[] rows)
        => writer.WriteRows(x + XOffset, y + YOffset, rows);

    /// <inheritdoc />
    public void WriteCell<T>(T content, OpenDocumentStyle? style = null, int x = 0, int y = 0)
        => WriteCell(x, y, content, style);

    /// <inheritdoc />
    public void WriteRowStyle(int y, OpenDocumentStyle style)
        => writer.WriteRowStyle(y + YOffset, style);

    /// <inheritdoc />
    public void WriteColumn<T>(int x, int y, IEnumerable<T> content, OpenDocumentStyle? style = null)
        => writer.WriteColumn(x + XOffset, y + YOffset, content, style);

    /// <inheritdoc />
    public int WriteColumns<T>(int x, int y, IEnumerable<IEnumerable<T>> contents, OpenDocumentStyle? style = null)
        => writer.WriteColumns(x + XOffset, y + YOffset, contents, style);
}
