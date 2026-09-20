using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace OpenDocumentCreator;

/// <summary>
/// The table:table-cell element represents a table cell. It is contained in a table row element.
/// A table cell can contain paragraphs and other text content as well as sub tables. Table cells may
///  span multiple columns and rows. Table cells may be empty.
/// </summary>
/// <remarks>
/// Missing:
/// --------
/// The `table:table-cell` element has the following attributes:
///    * office:boolean-value 19.367,
///    * office:currency 19.369,
///    * office:date-value 19.370,
///    * office:string-value 19.379,
///    * office:time-value 19.382,
///    * office:value 19.384,
///    * table:content-validation-name 19.601,
///    * table:number-matrix-columns-spanned 19.679,
///    * table:number-matrix-rows-spanned 19.680,
///    * table:protect 19.695,
///    * table:protected 19.696.5,
///    * xhtml:about 19.905,
///    * xhtml:content 19.906,
///    * xhtml:datatype 19.907,
///    * xhtml:property 19.908 and
///    * xml:id 19.914.
///
/// The `table:table-cell` element has the following child elements:
///    * dr3d:scene 10.5.2,
///    * draw:a 10.4.12,
///    * draw:caption 10.3.11,
///    * draw:circle 10.3.8,
///    * draw:connector 10.3.10,
///    * draw:control 10.3.13,
///    * draw:custom-shape 10.6.1,
///    * draw:ellipse 10.3.9,
///    * draw:g 10.3.15,
///    * draw:line 10.3.3,
///    * draw:measure 10.3.12,
///    * draw:page-thumbnail 10.3.14,
///    * draw:path 10.3.7,
///    * draw:polygon 10.3.5,
///    * draw:polyline 10.3.4,
///    * draw:rect 10.3.2,
///    * draw:regular-polygon 10.3.6,
///    * office:annotation 14.1,
///    * table:cell-range-source 9.3.1,
///    * table:detective 9.3.2,
///    * table:table 9.1.2,
///    * text:alphabetical-index 8.8,
///    * text:bibliography 8.9,
///    * text:change 5.5.7.4,
///    * text:change-end 5.5.7.3,
///    * text:change-start 5.5.7.2,
///    * text:h 5.1.2,
///    * text:illustration-index 8.4,
///    * text:list 5.3.1,
///    * text:numbered-paragraph 5.3.6,
///    * text:object-index 8.6,
///    * text:p 5.1.3,
///    * text:section 5.4,
///    * text:soft-page-break 5.6,
///    * text:table-index 8.5,
///    * text:table-of-content 8.3 and
///    * text:user-index 8.7.
///    </remarks>
internal class OpenDocumentTableCell : OpenDocumentWritable
{
    /// <inheritdoc />
    internal override string OpenDocumentElementName => "table-cell";

    /// <inheritdoc />
    internal override string? NamespaceName => "table";

    /// <summary>
    /// table:style-name 19.726.13,
    /// </summary>
    [OpenDocumentName("style-name", ElemNamespace = "table")]
    public string? StyleName { get; set; } = null;

    /// <summary>
    /// office:value-type 19.385,
    /// </summary>
    [OpenDocumentName("value-type", ElemNamespace = "office")]
    public string? ValueType { get; set; } = null;

    /// <summary>
    /// 19.642
    /// The table:formula attribute specifies a formula for a table cell.
    ///
    /// Formulas specify calculations to be performed within table cells.The attribute value should begin
    /// with a namespace prefix followed by ":" (U+003A, COLON), followed by the text of the formula.
    ///
    /// The namespace bound to the prefix determines the syntax and semantics of the formula.
    /// Whenever the initial text of a formula has the appearance of an NCName followed by ":" (U+003A,
    /// COLON), an OpenDocument producer shall provide a valid namespace prefix and separating ":"
    /// (U+003A, COLON) separator before the text of the formula in order to eliminate any ambiguity.
    /// </summary>
    [OpenDocumentName("formula", ElemNamespace = "table")]
    public string? Formula { get; set; } = null;

    /// <summary>
    /// office:value 19.384
    ///
    /// The office:value attribute specifies the currency, float or percentage value for a table cell
    /// </summary>
    [OpenDocumentName("value", ElemNamespace = "office")]
    public double? Value { get; set; } = null;

    /// <summary>
    /// 19.675.3
    /// default value is 1, so one means no repetition
    /// </summary>
    [OpenDocumentName("number-columns-repeated", ElemNamespace = "table")]
    public int? NumberColumnsRepeated { get; set; } = null;

    /// <summary>
    /// table:number-columns-spanned 19.676,
    /// </summary>
    [OpenDocumentName("number-columns-spanned", ElemNamespace = "table")]
    public int? NumberColumnsSpanned { get; set; } = null;

    /// <summary>
    /// table:number-rows-spanned 19.678,
    /// </summary>
    [OpenDocumentName("number-rows-spanned", ElemNamespace = "table")]
    public int? NumberRowsSpanned { get; set; } = null;

    /// <summary>
    ///     draw:frame 10.4.2,
    /// </summary>
    [OpenDocumentName]
    public OpenDocumentFrame? Frame { get; set; } = null;


    /// <inheritdoc />
    internal override void WriteTo(XmlWriter writer, Action<XmlWriter>? extraAttributes, Action<XmlWriter>? extraChildren)
    {
        ArgumentNullException.ThrowIfNull(writer, nameof(writer));

        var table = OpenDocument.Table.NamespaceName;
        var office = OpenDocument.Office.NamespaceName;

        writer.WriteStartElement("table", OpenDocumentElementName, table);

        if (NumberColumnsRepeated is not null)
        {
            writer.WriteAttributeString("table", "number-columns-repeated", table, NumberColumnsRepeated.Value.ToString(CultureInfo.InvariantCulture));
        }
        if (NumberColumnsSpanned is not null)
        {
            writer.WriteAttributeString("table", "number-columns-spanned", table, NumberColumnsSpanned.Value.ToString(CultureInfo.InvariantCulture));
        }
        if (NumberRowsSpanned is not null)
        {
            writer.WriteAttributeString("table", "number-rows-spanned", table, NumberRowsSpanned.Value.ToString(CultureInfo.InvariantCulture));
        }
        if (StyleName is not null)
        {
            writer.WriteAttributeString("table", "style-name", table, StyleName);
        }
        if (ValueType is not null)
        {
            writer.WriteAttributeString("office", "value-type", office, ValueType);
        }
        if (Formula is not null)
        {
            writer.WriteAttributeString("table", "formula", table, Formula);
        }
        if (Value is not null)
        {
            writer.WriteAttributeString("office", "value", office, XmlConvert.ToString(Value.Value));
        }
        extraAttributes?.Invoke(writer);

        if (Frame is not null)
        {
            Frame.WriteTo(writer);
        }
        extraChildren?.Invoke(writer);

        writer.WriteEndElement();
    }
}
