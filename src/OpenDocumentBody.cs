using System.Xml;
using System.Xml.Linq;

namespace OpenDocumentCreator;

/// <summary>
/// The office:body element contains the elements that represent the content of a document.
///
///
/// The office:body element has the following child elements:
/// * office:chart 3.8,
/// * office:database 12.1,
/// * office:drawing 3.5,
/// * office:image 3.9,
/// * office:presentation 3.6,
/// * office:spreadsheet 3.7
/// * office:text 3.4.
/// </summary>
internal class OpenDocumentBody : OpenDocumentWritable
{
    /// <inheritdoc />
    internal override string OpenDocumentElementName => "body";

    /// <inheritdoc />
    internal override string? NamespaceName => "office";

    /// <summary>
    /// Any of:
    ///
    /// * office:chart 3.8,
    /// * office:database 12.1,
    /// * office:drawing 3.5,
    /// * office:image 3.9,
    /// * office:presentation 3.6,
    /// * office:spreadsheet 3.7,
    /// * office:text 3.4.
    /// </summary>
    [OpenDocumentName("content")]
    public XElement? Content { get; set; } = null;

    /// <summary>
    /// Writes the body's content directly, instead of it being handed over as a built
    /// <see cref="Content"/> element. Set for the streaming save path; when null the body
    /// serializes <see cref="Content"/> the usual way.
    /// </summary>
    internal Action<XmlWriter>? ContentWriter { get; set; }

    /// <inheritdoc />
    internal override void WriteTo(XmlWriter writer, Action<XmlWriter>? extraAttributes, Action<XmlWriter>? extraChildren)
    {
        if (ContentWriter is null)
        {
            base.WriteTo(writer, extraAttributes, extraChildren);
            return;
        }

        ArgumentNullException.ThrowIfNull(writer, nameof(writer));

        writer.WriteStartElement("office", OpenDocumentElementName, OpenDocument.Office.NamespaceName);
        extraAttributes?.Invoke(writer);
        ContentWriter(writer);
        extraChildren?.Invoke(writer);
        writer.WriteEndElement();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenDocumentBody"/> class.
    /// </summary>
    public OpenDocumentBody()
    {
    }
}
