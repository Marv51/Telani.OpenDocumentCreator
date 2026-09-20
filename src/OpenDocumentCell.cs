using System.Buffers;
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
    /// Writes one text:p.
    /// </summary>
    /// <param name="writer">the writer to write to</param>
    /// <param name="content">the paragraph text</param>
    private static void WriteParagraph(XmlWriter writer, string content)
    {
        writer.WriteStartElement("text", "p", Text.NamespaceName);
        WriteEncodedText(writer, content);
        writer.WriteEndElement();
    }

    /// <summary>
    /// Writes text, carrying runs of more than one space as text:s elements.
    /// </summary>
    /// <param name="writer">the writer to write to</param>
    /// <param name="text">the text to write</param>
    /// <remarks>
    /// A single space stays literal; a run of n spaces is written as one space followed by a
    /// text:s with a count of n-1, which is how the format keeps them from being collapsed.
    ///
    /// The segments are handed to the writer as spans of the original string, so nothing is
    /// copied. This used to build a list of XText and XElement nodes and a StringBuilder first,
    /// which cost over a kilobyte for a cell holding a couple of double spaces.
    /// </remarks>
    private static void WriteEncodedText(XmlWriter writer, string text)
    {
        // fast path: no double spaces (the string literal is two spaces)
        if (!text.Contains("  ", StringComparison.Ordinal))
        {
            writer.WriteString(text);
            return;
        }

        // WriteChars escapes exactly as WriteString does, and takes a range, so the segments can
        // be written out of one pooled buffer instead of being cut out as separate strings.
        var buffer = ArrayPool<char>.Shared.Rent(text.Length);
        try
        {
            text.CopyTo(buffer);
            var span = text.AsSpan();

            var segmentStart = 0;
            var i = 0;

            while (i < span.Length)
            {
                if (span[i] != ' ')
                {
                    i++;
                    continue;
                }

                var runStart = i;
                while (i < span.Length && span[i] == ' ')
                {
                    i++;
                }

                if (i - runStart <= 1)
                {
                    continue;
                }

                // the run's first space stays with the text before it
                writer.WriteChars(buffer, segmentStart, runStart + 1 - segmentStart);

                if (i == span.Length)
                {
                    // A run that ends the text is written as that one space and no more, which is
                    // what this produced when it went through the element model.
                    return;
                }

                writer.WriteStartElement("text", "s", Text.NamespaceName);
                writer.WriteAttributeString("text", "c", Text.NamespaceName, (i - runStart - 1).ToString(CultureInfo.InvariantCulture));
                writer.WriteEndElement();
                segmentStart = i;
            }

            if (segmentStart < span.Length)
            {
                writer.WriteChars(buffer, segmentStart, span.Length - segmentStart);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
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
