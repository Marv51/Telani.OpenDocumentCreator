using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using OpenDocumentCreator.Styles;

namespace OpenDocumentCreator;

/// <summary>
/// A cell in an open document spreadsheet.
/// </summary>
public class OpenDocumentCell
{
    // private static readonly XNamespace table = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:table:1.0");
    private static readonly XNamespace Text = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:text:1.0");
    private static readonly XNamespace Office = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:office:1.0");
    private static readonly XNamespace Xlink = XNamespace.Get("http://www.w3.org/1999/xlink");

    /// <summary>
    /// The style of this cell.
    /// </summary>
    public OpenDocumentStyle? Style { get; set; }

    /// <summary>
    /// The text content of this cell.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Controls how empty lines within <see cref="Content"/> are rendered. Defaults to
    /// <see cref="EmptyLineHandling.Collapse"/>, which preserves the historical behaviour
    /// of dropping consecutive, leading and trailing line breaks.
    /// </summary>
    public EmptyLineHandling EmptyLines { get; set; } = EmptyLineHandling.Collapse;

    /// <summary>
    /// The numeric content of this cell.
    ///
    /// ODF names the value type "float", but declares office:value as xs:double, so this is a
    /// double: values are stored with the full precision the format allows.
    /// </summary>
    public double? FloatContent { get; set; }

    /// <summary>
    /// The formula of this cell. (You also need to set the FloatContent or Content with the result)
    /// </summary>
    public string? Formula { get; set; }

    /// <summary>
    /// Repeats a cell 'n' number of times in the particular row
    /// Can't be 0 or negative
    /// </summary>
    public int NumberColumnsRepeated { get; set; } = 1;

    /// <summary>
    /// Can't be 0 or negative
    /// </summary>
    public int ColumnsSpanned { get; set; } = 1;

    /// <summary>
    /// Can't be 0 or negative
    /// </summary>
    public int RowsSpanned { get; set; } = 1;

    /// <summary>
    /// Is this cell covered? Covered means a neighboring cell spanns "over" this cell.
    /// </summary>
    public bool IsCovered { get; set; }

    /// <summary>
    /// A Uri to use as a clickable link in this cell
    /// </summary>
    public Uri? Link { get; set; }

    /// <summary>
    /// A frame, that is a movable text box or image that is anchored in this cell.
    /// </summary>
    public OpenDocumentFrame? Frame { get; set; }

    private static readonly string[] Separator = ["\n"];

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenDocumentCell"/> class.
    /// </summary>
    /// <param name="c">The string content</param>
    /// <param name="style">the style of the cell</param>
    public OpenDocumentCell(string? c, OpenDocumentStyle style)
        : this(c) => Style = style;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenDocumentCell"/> class.
    /// </summary>
    /// <param name="c">the string content</param>
    public OpenDocumentCell(string? c) => Content = c;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenDocumentCell"/> class.
    /// </summary>
    /// <param name="c">the string content</param>
    /// <param name="style">the style of the cell</param>
    public OpenDocumentCell(double c, OpenDocumentStyle style)
        : this(c) => Style = style;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenDocumentCell"/> class.
    /// </summary>
    /// <param name="c"></param>
    public OpenDocumentCell(double c) => FloatContent = c;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenDocumentCell"/> class.
    /// </summary>
    /// <param name="link">A uri that should be inserted as a link.</param>
    public OpenDocumentCell(Uri link) => Link = link;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenDocumentCell"/> class.
    /// </summary>
    /// <param name="link">A uri that should be inserted as a link.</param>
    /// <param name="style">The style of this cell</param>
    public OpenDocumentCell(Uri link, OpenDocumentStyle style)
        : this(link) => Style = style;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenDocumentCell"/> class.
    /// </summary>
    public OpenDocumentCell()
    {
    }

    private IReadOnlyList<string> SplitContentLines(string content)
    {
        if (EmptyLines == EmptyLineHandling.Collapse)
        {
            return content.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        }

        var lines = new List<string>(content.Split(Separator, StringSplitOptions.None));

        if (EmptyLines == EmptyLineHandling.TrimEnds)
        {
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
            {
                lines.RemoveAt(0);
            }
            while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.RemoveAt(lines.Count - 1);
            }
        }

        return lines;
    }

    private static XNode[] EncodeTextContent(string the_text)
    {
        // fast path: no double spaces (the string literal is two spaces)
        if (!the_text.Contains("  "))
        {
            return [new XText(the_text)];
        }

        var elems = new List<XNode>();
        var builder = new StringBuilder();
        var numberOfPreviousSpaces = 0;

        for (var i = 0; i < the_text.Length; i++)
        {
            if (the_text[i] == ' ')
            {
                if (numberOfPreviousSpaces == 0)
                {
                    builder.Append(' ');
                }
                numberOfPreviousSpaces++;
            }
            else
            {
                if (numberOfPreviousSpaces > 1)
                {
                    elems.Add(new XText(builder.ToString()));

                    elems.Add(new XElement(Text + "s", new XAttribute(Text + "c", numberOfPreviousSpaces - 1)));
                    builder.Clear();
                }
                numberOfPreviousSpaces = 0;
                builder.Append(the_text[i]);
            }
        }
        if (builder.Length > 0)
        {
            elems.Add(new XText(builder.ToString()));
        }

        return [.. elems];
    }

    /// <summary>
    /// Formats a value for the office:value attribute.
    ///
    /// ODF declares that attribute as xs:double, whose lexical space spells the non finite values
    /// INF, -INF and NaN. .NET writes "Infinity", "-Infinity" and "NaN" instead, and the first two
    /// are not valid xs:double, so they are mapped here. Finite values use the default round
    /// trippable form.
    /// </summary>
    /// <param name="value">the value to format</param>
    /// <returns>the lexical representation for office:value</returns>
    private static string FormatCellValue(double value)
    {
        if (double.IsPositiveInfinity(value))
        {
            return "INF";
        }
        if (double.IsNegativeInfinity(value))
        {
            return "-INF";
        }
        if (double.IsNaN(value))
        {
            return "NaN";
        }
        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes this cell straight to <paramref name="writer"/>.
    /// </summary>
    /// <param name="writer">the writer to write to</param>
    /// <remarks>
    /// The streaming counterpart of <see cref="CreateElement"/>, and deliberately the same shape:
    /// the small table-cell object is still built, because it carries the attribute mapping, but
    /// no XElement tree is. There are a great many of these per document, and the tree was most of
    /// what an export allocated.
    /// </remarks>
    internal void WriteTo(XmlWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer, nameof(writer));

        var table = OpenDocument.Table.NamespaceName;

        if (IsCovered)
        {
            writer.WriteStartElement("table", "covered-table-cell", table);
            writer.WriteEndElement();
            return;
        }

        var office = OpenDocument.Office.NamespaceName;

        writer.WriteStartElement("table", "table-cell", table);

        // Attribute order is the order the table-cell element declares them, because that is the
        // order they came out in when this went through one.
        if (NumberColumnsRepeated != 1)
        {
            writer.WriteAttributeString("table", "number-columns-repeated", table, NumberColumnsRepeated.ToString(CultureInfo.InvariantCulture));
        }
        if (ColumnsSpanned != 1 || RowsSpanned != 1)
        {
            writer.WriteAttributeString("table", "number-columns-spanned", table, ColumnsSpanned.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("table", "number-rows-spanned", table, RowsSpanned.ToString(CultureInfo.InvariantCulture));
        }
        writer.WriteAttributeString("table", "style-name", table, Style is not null ? Style.Name : "ce1");

        if (Link is not null)
        {
            var realLink = Link.ToString();
            if (realLink.StartsWith("sheet://", StringComparison.InvariantCultureIgnoreCase))
            {
                realLink = realLink[("sheet://".Length + 1)..];
            }

            writer.WriteAttributeString("office", "value-type", office, "string");

            writer.WriteStartElement("text", "p", Text.NamespaceName);
            writer.WriteStartElement("text", "a", Text.NamespaceName);
            writer.WriteAttributeString("xlink", "href", Xlink.NamespaceName, realLink);
            writer.WriteAttributeString("xlink", "type", Xlink.NamespaceName, "simple");
            writer.WriteString(string.IsNullOrEmpty(Content) ? realLink.TrimEnd('/') : Content);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        else if (!string.IsNullOrEmpty(Formula))
        {
            if (FloatContent.HasValue)
            {
                writer.WriteAttributeString("office", "value-type", office, "float");
                writer.WriteAttributeString("table", "formula", table, Formula);
                WriteFloatValue(writer);
                WriteFloatParagraph(writer);
            }
            else if (!string.IsNullOrEmpty(Content))
            {
                writer.WriteAttributeString("office", "value-type", office, "string");
                writer.WriteAttributeString("table", "formula", table, Formula);
                WriteParagraph(writer, Content);
            }
            else
            {
                Debug.Fail("No value provided for the result of the formula");
                writer.WriteAttributeString("table", "formula", table, Formula);
            }
        }
        else if (!string.IsNullOrEmpty(Content))
        {
            writer.WriteAttributeString("office", "value-type", office, "string");

            if (Content.Contains('\n'))
            {
                foreach (var line in SplitContentLines(Content))
                {
                    WriteParagraph(writer, line);
                }
            }
            else
            {
                WriteParagraph(writer, Content);
            }
        }
        else if (FloatContent.HasValue)
        {
            writer.WriteAttributeString("office", "value-type", office, "float");
            WriteFloatValue(writer);
            WriteFloatParagraph(writer);
        }
        else if (Frame is not null)
        {
            Frame.WriteTo(writer);
        }

        writer.WriteEndElement();
    }

    // Through FormatCellValue, the same as the element model: the non finite values have their
    // own spelling in xs:double, and the two paths have to agree on it.
    private void WriteFloatValue(XmlWriter writer)
        => writer.WriteAttributeString("office", "value", Office.NamespaceName, FormatCellValue(FloatContent!.Value));

    private void WriteFloatParagraph(XmlWriter writer)
    {
        writer.WriteStartElement("text", "p", Text.NamespaceName);
        writer.WriteString(FloatContent!.Value.ToString(CultureInfo.CurrentCulture));
        writer.WriteEndElement();
    }

    /// <summary>
    /// Writes one text:p, encoding runs of spaces as text:s exactly as the element model does.
    /// </summary>
    private static void WriteParagraph(XmlWriter writer, string content)
    {
        writer.WriteStartElement("text", "p", Text.NamespaceName);
        foreach (var node in EncodeTextContent(content))
        {
            if (node is XElement spaceRun)
            {
                writer.WriteStartElement("text", spaceRun.Name.LocalName, Text.NamespaceName);
                foreach (var attribute in spaceRun.Attributes())
                {
                    writer.WriteAttributeString("text", attribute.Name.LocalName, Text.NamespaceName, attribute.Value);
                }
                writer.WriteEndElement();
            }
            else if (node is XText text)
            {
                // Value, not ToString: ToString serializes the node through a writer of its own,
                // which both escapes the text a second time and allocates a writer per text node.
                writer.WriteString(text.Value);
            }
        }
        writer.WriteEndElement();
    }

    /// <summary>
    /// This cell as an XML element.
    /// </summary>
    /// <returns>the table-cell element for this cell</returns>
    /// <remarks>
    /// Built by running <see cref="WriteTo(XmlWriter)"/> into a writer that appends to an XLinq
    /// tree. A cell has the most branching of any element here - links, formulas, numbers,
    /// multi line text, frames - and describing that twice is how the two paths came to spell the
    /// non finite numbers differently. Now there is one description.
    /// </remarks>
    internal XElement CreateElement()
    {
        var document = new XDocument();
        using (var writer = document.CreateWriter())
        {
            WriteTo(writer);
        }
        return document.Root!;
    }
}
