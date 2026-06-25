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
    /// The float content of this cell.
    /// </summary>
    public float? FloatContent { get; set; }

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
    public OpenDocumentCell(double c) => FloatContent = Convert.ToSingle(c);

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

    internal XElement CreateElement()
    {
        if (IsCovered)
        {
            return OpenDocument.GetElementFor(new OpenDocumentCoveredTableCell());
        }
        else
        {
            var cellNode = new OpenDocumentTableCell()
            {
                StyleName = Style is not null ? Style.Name : "ce1",
            };
            Debug.Assert(ColumnsSpanned > 0);
            Debug.Assert(RowsSpanned > 0);
            if (ColumnsSpanned != 1 || RowsSpanned != 1)
            {
                cellNode.NumberColumnsSpanned = ColumnsSpanned;
                cellNode.NumberRowsSpanned = RowsSpanned;
            }
            Debug.Assert(NumberColumnsRepeated > 0);
            if (NumberColumnsRepeated != 1)
            {
                cellNode.NumberColumnsRepeated = NumberColumnsRepeated;
            }
            XElement elem;
            if (Link is not null)
            {
                cellNode.ValueType = "string";
                elem = OpenDocument.GetElementFor(cellNode);

                var realLink = Link.ToString();
                if (realLink.StartsWith("sheet://", StringComparison.InvariantCultureIgnoreCase))
                {
                    realLink = realLink[("sheet://".Length + 1)..];
                }

                var linkNode = new XElement(Text + "a", string.IsNullOrEmpty(Content) ? realLink.TrimEnd('/') : Content);
                linkNode.SetAttributeValue(Xlink + "href", realLink);
                linkNode.SetAttributeValue(Xlink + "type", "simple");
                elem.Add(new XElement(Text + "p", linkNode));
            }
            else if (!string.IsNullOrEmpty(Formula))
            {
                if (FloatContent.HasValue)
                {
                    cellNode.ValueType = "float";
                    cellNode.Formula = Formula;
                    elem = OpenDocument.GetElementFor(cellNode);
                    elem.Add(new XAttribute(Office + "value", FloatContent.Value.ToString(CultureInfo.InvariantCulture)));
                    elem.Add(new XElement(Text + "p", new XText(FloatContent.Value.ToString(CultureInfo.CurrentCulture))));
                }
                else if (!string.IsNullOrEmpty(Content))
                {
                    cellNode.ValueType = "string";
                    cellNode.Formula = Formula;
                    elem = OpenDocument.GetElementFor(cellNode);
                    elem.Add(new XElement(Text + "p", EncodeTextContent(Content)));
                }
                else
                {
                    Debug.Fail("No value provided for the result of the formula");
                    cellNode.Formula = Formula;
                    elem = OpenDocument.GetElementFor(cellNode);
                }
            }
            else if (!string.IsNullOrEmpty(Content))
            {
                cellNode.ValueType = "string";
                elem = OpenDocument.GetElementFor(cellNode);
                if (Content.Contains('\n'))
                {
                    foreach (var line in SplitContentLines(Content))
                    {
                        elem.Add(new XElement(Text + "p", EncodeTextContent(line)));
                    }
                }
                else
                {
                    elem.Add(new XElement(Text + "p", EncodeTextContent(Content)));
                }
            }
            else if (FloatContent.HasValue)
            {
                cellNode.ValueType = "float";
                elem = OpenDocument.GetElementFor(cellNode);
                elem.Add(new XAttribute(Office + "value", FloatContent.Value.ToString(CultureInfo.InvariantCulture)));
                elem.Add(new XElement(Text + "p", new XText(FloatContent.Value.ToString(CultureInfo.CurrentCulture))));
            }
            else
            {
                cellNode.Frame = Frame;
                elem = OpenDocument.GetElementFor(cellNode);
            }
            return elem;
        }
    }
}
