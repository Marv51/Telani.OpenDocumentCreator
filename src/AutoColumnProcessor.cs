using System.Globalization;
using System.Text;
using OpenDocumentCreator.Styles;

namespace OpenDocumentCreator;

/// <summary>
/// AutoColumnProcessor is an abstraction on top of the plain column system.
///
/// Columns really only have one important property: their width.
///
/// So instead of creating styles manually and then creating column
/// elements individually each with the appropriate style, with AutoColumnProcessor, you
/// can just pass a List of AutoColumn objects into the Process method.
///
/// To create AutoColumn objects very quickly they can be parsed from a
/// string representation using the ParseTemplateString method.
/// </summary>
public static class AutoColumnProcessor
{
    private static List<Column> Process(OpenDocument doc, IList<AutoColumn> columns)
    {
        foreach (var c in columns)
        {
            c.Column.StyleName = doc.GetOrAddTableColumnStyle(c.Properties).Name;
        }
        return columns.Select(a => a.Column).ToList();
    }

    /// <summary>
    /// Apply (or convert) a list of AutoColumn objects to regular columns on the table passed as an argument.
    ///
    ///  You can call this multiple times for the same table or on a table with existing columns, as it only
    ///  appends the corresponding Column objects.
    /// </summary>
    /// <param name="doc">The document that is being worked on (needed to access the styles)</param>
    /// <param name="table">The table the columns should be added to</param>
    /// <param name="autoColumns">The AutoColumn objects that represent how the table should look.</param>
    /// <exception cref="System.ArgumentNullException">If <paramref name="table"/>, <paramref name="doc"/> or <paramref name="autoColumns"/> are empty.</exception>
    public static void Apply(OpenDocument doc, OpenDocumentTable table, IList<AutoColumn> autoColumns)
    {
        ArgumentNullException.ThrowIfNull(table, nameof(table));
        ArgumentNullException.ThrowIfNull(doc, nameof(doc));
        ArgumentNullException.ThrowIfNull(autoColumns, nameof(autoColumns));

        var columns = Process(doc, autoColumns);
        table.AddColumns(columns);
    }

    private static readonly char[] Separator = ['|'];

    /// <summary>
    /// This parses a string into a number of AutoColumns.
    /// The string might look like this "|60mm|20mm|60mm|60mm|40mm|30mm|60mm|40mm|60mm|"
    /// A vertical bar between each column. Width of a column is defined as a number followed by mm.
    ///
    /// White spaces around the "|" character are allowed.
    /// </summary>
    /// <param name="template">String to parse</param>
    /// <returns>the parsed AutoColumns</returns>
    /// <exception cref="System.ArgumentNullException">If <paramref name="template"/> is null.</exception>
    public static IList<AutoColumn> ParseTemplateString(string template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var columns = new List<AutoColumn>();

        var parsed_columns = template.Trim().Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var c in parsed_columns)
        {
            columns.Add(new AutoColumn(ParseMeasurement(c.Trim())));
        }

        return columns;
    }

    /// <summary>
    /// Create multiple columns at once.
    /// </summary>
    /// <param name="nColumns">The number of columns</param>
    /// <param name="measurement">The width of the columns</param>
    /// <returns>The newly added columns.</returns>
    public static IList<AutoColumn> CreateColumns(int nColumns, string measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement, nameof(measurement));

        return Enumerable.Range(0, nColumns).Select(i => new AutoColumn(ParseMeasurement(measurement))).ToList();
    }

    private static Measurement ParseMeasurement(string v)
    {
        var result = decimal.Parse(v[0..^2], CultureInfo.InvariantCulture);
        return new Measurement(result, Unit.MM);
    }
}
