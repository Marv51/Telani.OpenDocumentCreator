using System.Xml;
using System.Xml.Linq;

namespace OpenDocumentCreator;

/// <summary>
/// The table:table-row element represents a row in a table.
///
/// It contains elements that specify the cells of the table row.
///
/// MISSING:
/// =======
/// * table:default-cell-style-name 19.615,
/// * table:style-name 19.726.15,
/// * table:visibility 19.749
/// * xml:id 19.914.
///
/// The table:table-row element has the following child elements:
/// * table:covered-table-cell 9.1.5
///
///
/// </summary>
internal sealed class OpenDocumentTableRow : OpenDocumentWritable
{
    /// <summary>
    ///  Name of the XML tag
    /// </summary>
    internal override string OpenDocumentElementName => "table-row";

    /// <summary>
    /// Namespace of the XML tag
    /// </summary>
    internal override string? NamespaceName => "table";

    /// <summary>
    /// 19.677
    ///
    /// The table:number-rows-repeated attribute specifies the number of rows to which a row
    /// element applies. If two or more rows are adjoining, and have the same content and style, and do
    /// not contain vertically merged cells, they may be described by a single table:table-row
    /// element that has a table:number-rows-repeated attribute with a value greater than 1.
    ///
    /// The default value for this attribute is 1.
    ///
    /// Attribute has the data type positiveInteger 18.2.
    /// </summary>
    [OpenDocumentName("number-rows-repeated", ElemNamespace = "table")]
    public string? NumberRowsRepeated { get; set; } = null;

    /// <summary>
    /// 19.726.15
    ///
    /// The table:style-name attribute specifies a style:style element of type table-row.
    /// </summary>
    [OpenDocumentName("style-name", ElemNamespace = "table")]
    public string? StyleName { get; set; } = null;

    /// <summary>
    /// 9.1.4
    ///
    /// The table:table-cell element represents a table cell. It is contained in a table row
    /// element. A table cell can contain paragraphs and other text content as well as sub tables. Table
    /// cells may span multiple columns and rows. Table cells may be empty.
    /// </summary>
    [OpenDocumentName]
    public List<OpenDocumentTableCell> TableCells { get; private set; } = [];

    /// <inheritdoc />
    internal override void WriteTo(XmlWriter writer, Action<XmlWriter>? extraAttributes, Action<XmlWriter>? extraChildren)
    {
        ArgumentNullException.ThrowIfNull(writer, nameof(writer));

        writer.WriteStartElement("table", OpenDocumentElementName, OpenDocument.Table.NamespaceName);

        if (NumberRowsRepeated is not null)
        {
            writer.WriteAttributeString("table", "number-rows-repeated", OpenDocument.Table.NamespaceName, NumberRowsRepeated);
        }
        if (StyleName is not null)
        {
            writer.WriteAttributeString("table", "style-name", OpenDocument.Table.NamespaceName, StyleName);
        }
        extraAttributes?.Invoke(writer);

        foreach (var item in TableCells)
        {
            item.WriteTo(writer);
        }
        extraChildren?.Invoke(writer);

        writer.WriteEndElement();
    }
}
