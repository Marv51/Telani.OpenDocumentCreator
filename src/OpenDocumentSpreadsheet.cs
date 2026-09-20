using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace OpenDocumentCreator;

/// <summary>
/// A OpenDocument Spreadsheet, like you might create in Excel or similar apps.
/// </summary>
public sealed class OpenDocumentSpreadsheet(string creatorName = "") : OpenDocument(creatorName)
{
    /// <summary>
    /// The main tables, those little tabs at the bottom of Excel.
    /// </summary>
    /// <value>A collection of tables</value>
    public ICollection<OpenDocumentTable> Tables { get; } = [];

    /// <inheritdoc />
    protected override string GetMimeType() => "application/vnd.oasis.opendocument.spreadsheet";

    /// <inheritdoc />
    protected override XElement CreateContent()
    {
        // at least one table is required
        if (Tables.Count == 0)
        {
            throw new InvalidOperationException("At least one table is required to generate a valid ods file");
        }

        // LibreOffice supports at most 10000 sheets.

        // Check if any tables have the same name
        if (Tables.Select(s => s.Name).Distinct().Count() != Tables.Count)
        {
            // Lets get a good error message
            var duplicate = Tables.GroupBy(x => x.Name).Where(g => g.Count() > 1).First();
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Table Names must be distinct. (Two or more tables have the same name: \"{0}\")", duplicate.Key));
        }

        XElement spreadsheet = new(Office + "spreadsheet");

        spreadsheet.Add(GetElementFor(new OpenDocumentCalculationSettings()
        {
            CaseSensitive = OpenDocBoolean.False,
            SearchCriteriaMustApplyToWholeCell = OpenDocBoolean.True,
            UseWildcards = OpenDocBoolean.True,
            UseRegularExpressions = OpenDocBoolean.False,
            AutomaticFindLabels = OpenDocBoolean.False,
        }));

        foreach (var t in Tables)
        {
            // The padding is put back as soon as the columns have been serialized, so that saving
            // leaves the document exactly as it found it.
            XElement tableNode;
            using (t.PadColumnsWhileSerializing())
            {
                tableNode = GetElementFor(t);
            }

            foreach (var r in t.Rows)
            {
                var rowNodeElem = new OpenDocumentTableRow()
                {
                    StyleName = r.Style is null ? "ro1" : r.Style.Name,
                };
                var rowNode = OpenDocument.GetElementFor(rowNodeElem);
                foreach (var c in r.Cells)
                {
                    var cellNode = c.CreateElement();
                    rowNode.Add(cellNode);
                }
                rowNode.Add(GetElementFor(new OpenDocumentTableCell()
                {
                    NumberColumnsRepeated = 16384 - r.Cells.Select(cell => cell.NumberColumnsRepeated).Sum(),
                }));

                tableNode.Add(rowNode);
            }

            var a = new OpenDocumentTableRow()
            {
                NumberRowsRepeated = (1048576 - t.Rows.Count).ToString(CultureInfo.InvariantCulture),
                StyleName = "ro1",
            };
            a.TableCells.Add(new OpenDocumentTableCell()
            {
                NumberColumnsRepeated = 16384,
            });
            tableNode.Add(GetElementFor(a));
            spreadsheet.Add(tableNode);
        }
        return spreadsheet;
    }

    /// <summary>
    /// Returns a unique table name that does not collide with any existing table in this spreadsheet.
    /// The name is cropped to 31 characters (the Excel sheet name limit) and deduplicated
    /// by appending a numeric suffix (_1, _2, ...) if needed.
    /// </summary>
    /// <param name="desiredName">The desired (unescaped) table name, may exceed 31 characters.</param>
    /// <returns>A unique table name, at most 31 characters, safe for use as a sheet name.</returns>
    public string GetUniqueTableName(string desiredName)
    {
        var cropped = desiredName.Length > 31 ? desiredName[..31] : desiredName;

        var escaped = OpenDocumentTable.EscapeTableName(cropped);
        if (Tables.All(t => t.Name != escaped))
        {
            return cropped;
        }

        var index = 1;
        while (true)
        {
            var suffix = "_" + index.ToString(CultureInfo.InvariantCulture);
            var maxBase = 31 - suffix.Length;
            var candidate = (desiredName.Length > maxBase ? desiredName[..maxBase] : desiredName) + suffix;
            escaped = OpenDocumentTable.EscapeTableName(candidate);
            if (Tables.All(t => t.Name != escaped))
            {
                return candidate;
            }
            index++;
        }
    }

    /// <inheritdoc />
    protected override void ValidationBeforeSave()
    {
        if (Tables.Count == 0)
        {
            Tables.Add(new OpenDocumentTable("Tabelle 1"));
        }
    }
}
