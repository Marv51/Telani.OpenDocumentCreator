using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace OpenDocumentCreator;

/// <summary>
/// The table:table element is the root element for a table.
/// </summary>
/// <remarks>
/// Missing:
/// --------
/// The table:table element has the following attributes:
/// * table:is-sub-table 19.655,
/// * table:print 19.693,
/// * table:print-ranges 19.694,
/// * table:protected 19.696.4,
/// * table:protection-key 19.697,
/// * table:protection-keydigest-
/// * algorithm 19.698,
/// * table:template-name 19.732,
/// * table:use-banding-columns-styles 19.736,
/// * table:use-banding-rowsstyles 19.737,
/// * table:use-first-column-styles 19.738,
/// * table:use-first-rowstyles 19.739,
/// * table:use-last-column-styles 19.740,
/// * table:use-last-rowstyles 19.741 and
/// * xml:id 19.914.
///
/// The table:table element has the following child elements:
/// * office:dde-source 14.6.5,
/// * office:forms 13.2,
/// * table:desc 9.1.14,
/// * table:named-expressions 9.4.11,
/// * table:scenario 9.2.7,
/// * table:table-column-group 9.1.10,
/// * table:table-columns 9.1.12,
/// * table:table-header-columns 9.1.11,
/// * table:table-header-rows 9.1.7,
/// * table:table-row 9.1.3,
/// * table:table-row-group 9.1.9,
/// * table:table-rows 9.1.8,
/// * table:table-source 9.2.6,
/// * table:title 9.1.13,
/// * text:soft-page-break 5.6.
/// </remarks>
public class OpenDocumentTable : OpenDocumentWritable
{
    [return: NotNullIfNotNull(nameof(s))]
    private static string? CropToLength(string s, int length)
    {
        if (s is null)
        {
            return null;
        }
        return s.Length <= length ? s : s[..length];
    }

    /// <inheritdoc />
    internal override string OpenDocumentElementName => "table";

    /// <inheritdoc />
    internal override string? NamespaceName => "table";

    private readonly int excelColumnLimit = 16385;

    // private const int LIBREOFFICE_COLUMN_LIMIT = 1024;
    private string? _name;

    /// <summary>
    /// table:name 19.673.13,
    ///
    /// The table:name attribute specifies the name of a table.
    /// </summary>
    [OpenDocumentName("name", ElemNamespace = "table")]
    public string Name
    {
        get => _name ?? string.Empty;
        set
        {
            ArgumentNullException.ThrowIfNull(value, nameof(value));

            // Excel can't open files containing tables with names longer than 31 characters.
            if (value.Length > 31)
            {
                throw new ArgumentException("Table name too long (31 char. max)");
            }

            _name = string.IsNullOrEmpty(value) ? "Tabelle 1" : EscapeTableName(value);
        }
    }

    /// <summary>
    /// 9.2.8 table:shapes
    ///
    /// The table:shapes element contains all the elements that represent graphic shapes that are
    /// anchored on a table where this element occurs.
    ///
    /// This element can have many more types, however we currently only have Frame implemented.
    /// </summary>
    [OpenDocumentName]
    public OpenDocumentShapes? Shapes { get; set; } = null;

    /// <summary>
    /// table:style-name 19.726.16,
    ///
    /// The table:style-name attribute specifies a style:style element of type table.
    /// </summary>
    [OpenDocumentName("style-name", ElemNamespace = "table")]
    public string StyleName { get; set; } = "ta1";

    /// <summary>
    /// The rows of the table
    /// </summary>
    /// <value>list of rows</value>
    protected internal List<Row> Rows { get; private set; } = [];

    private readonly List<Column> _columns = [];

    /// <summary>
    /// table:table-column 9.1.6,
    ///
    /// The table:table-column element specifies properties for one or more adjacent columns in
    /// a table.
    /// </summary>
    [OpenDocumentName]
    public IList<Column> Columns => _columns;

    /// <summary>
    /// This finalizes the columns. Meaning any kind of final validation or cleaning up work before the document is saved.
    /// </summary>
    internal IDisposable PadColumnsWhileSerializing() => new ColumnPadding(this);

    /// <summary>
    /// Pads the table out to the column limit for as long as it is held, and puts the last column
    /// back the way it was when disposed.
    ///
    /// The padding is an artefact of the file format, not of the document: a reader expects the
    /// full width, but the table in memory has only the columns the caller added. Writing the
    /// padded value back permanently made saving change the document, so a second save of the same
    /// document counted the padding as real columns and produced a table that claimed a handful of
    /// columns while every row still held 16384 cells.
    /// </summary>
    private sealed class ColumnPadding : IDisposable
    {
        private readonly OpenDocumentTable table;
        private readonly Column? padded;
        private readonly string previousRepeated;

        internal ColumnPadding(OpenDocumentTable table)
        {
            this.table = table;

            if (table.Columns.Count == 0)
            {
                previousRepeated = string.Empty;
                return;
            }

            padded = table.Columns[^1];
            previousRepeated = padded.NumberColumnsRepeated;
            padded.NumberColumnsRepeated = (table.excelColumnLimit - table.TotalNumberOfColumns()).ToString(CultureInfo.InvariantCulture);
        }

        public void Dispose()
        {
            if (padded is not null)
            {
                padded.NumberColumnsRepeated = previousRepeated;
            }
        }
    }

    /// <summary>
    /// Add multiple columns to the document at once.
    /// </summary>
    /// <param name="columns">The columns to add</param>
    protected internal void AddColumns(IEnumerable<Column> columns)
    {
        ArgumentNullException.ThrowIfNull(columns, nameof(columns));

        foreach (var col in columns)
        {
            AddColumn(col);
        }
    }

    /// <summary>
    /// Add a specific new column <paramref name="c"/>.
    /// </summary>
    /// <param name="c">the new column to add.</param>
    /// <exception cref="System.InvalidOperationException">if this would exceed the maximum size of the table.</exception>
    public void AddColumn(Column c)
    {
        _columns.Add(c);
        if (TotalNumberOfColumns() > excelColumnLimit)
        {
            // it appears that there always have to be 16385 columns, not sure why, couldn't find a source
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Tables shall not have more than {0} Columns", excelColumnLimit));
        }
    }

    /// <summary>
    /// Add a column
    /// </summary>
    /// <param name="c">the style name for the new column</param>
    public void AddColumn(string c)
    {
        AddColumn(new Column(c));
    }

    /// <summary>
    /// Gets the total number of columns.
    /// </summary>
    /// <returns>the total number of columns.</returns>
    public int TotalNumberOfColumns()
    {
        var count = 0;
        if (_columns.Count > 0)
        {
            count = _columns.Select(x => x.NumberColumnsRepeated).Select(a => int.TryParse(a, out var b) ? b : 0).Aggregate((a, b) => a + b);
        }
        return count;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenDocumentTable"/> class with a given table name.
    /// </summary>
    /// <param name="name">The name of the table.</param>
    public OpenDocumentTable(string name)
    {
        Name = CropToLength(name, 31);
    }

    /// <summary>
    /// Escape a string suggestion, to a valid table name.
    /// </summary>
    /// <param name="unescaped">the unescaped string</param>
    /// <returns>the escaped string</returns>
    public static string EscapeTableName(string unescaped)
    {
        // According to https://support.microsoft.com/en-us/office/rename-a-worksheet-3f1f7148-ee83-404d-8ef0-9ff99fbad1f9
        // These are forbidden / \ ? * : [ ]
        var returnString = unescaped.Replace('[', '(');
        returnString = returnString.Replace(']', ')');
        returnString = returnString.Replace('*', '"');
        returnString = returnString.Replace('?', '!');
        returnString = returnString.Replace(':', ';');
        returnString = returnString.Replace('/', '|');
        returnString = returnString.Replace('\\', '|');

        // Begin or end with an apostrophe ('), but they can be used in between text or numbers in a name.
        // We just avoid apostrophes for maximum compatibility.
        returnString = returnString.Replace('\'', '|');

        // Reserved table name:
        if (returnString == "History")
        {
            return "Tabelle 1";
        }

        // Excel will escape periods and spaces on subsequent saves, without
        // updating any links. This can be considered an Excel bug, but
        // we work around that by avoiding those characters.
        returnString = returnString.Replace('.', ';');
        returnString = returnString.Replace(' ', '_');

        return returnString;
    }
}
