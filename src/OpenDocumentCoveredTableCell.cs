using System.Xml;
using System.Xml.Linq;

namespace OpenDocumentCreator;

internal sealed class OpenDocumentCoveredTableCell : OpenDocumentWritable
{
    internal override string OpenDocumentElementName => "covered-table-cell";

    internal override string? NamespaceName => "table";

    /// <inheritdoc />
    internal override void WriteTo(XmlWriter writer, Action<XmlWriter>? extraAttributes, Action<XmlWriter>? extraChildren)
    {
        ArgumentNullException.ThrowIfNull(writer, nameof(writer));

        writer.WriteStartElement("table", OpenDocumentElementName, OpenDocument.Table.NamespaceName);
        extraAttributes?.Invoke(writer);
        extraChildren?.Invoke(writer);
        writer.WriteEndElement();
    }
}
