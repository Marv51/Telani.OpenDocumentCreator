using System.Text;
using System.Xml;
using System.Xml.Linq;
using OpenDocumentCreator.Styles;

namespace OpenDocumentCreator.Tests;

/// <summary>
/// The streaming writer has to produce exactly what the element model produces. A silent
/// difference here would corrupt saved files without failing anything else, so every element kind
/// that matters is written both ways and the two strings are compared.
/// </summary>
[TestClass]
public sealed class StreamingSerializationEquivalenceTests
{
    private static string ViaElementModel(OpenDocumentWritable writable)
        => writable.GetElement().ToString(SaveOptions.DisableFormatting);

    private static string ViaStreaming(OpenDocumentWritable writable)
    {
        var builder = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = false,
            OmitXmlDeclaration = true,
            ConformanceLevel = ConformanceLevel.Fragment,
        };

        using (var writer = XmlWriter.Create(builder, settings))
        {
            writable.WriteTo(writer);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Compares the two serializations structurally rather than as text.
    ///
    /// A standalone element carries its own namespace declaration, and the two paths spell that
    /// differently: the element model emits a default xmlns, a writer emits a prefix. Inside a real
    /// document both use the prefixes declared on the root, so that difference never appears in a
    /// saved file. XNode.DeepEquals compares expanded names, attributes and content, which is what
    /// actually has to match.
    /// </summary>
    /// <param name="writable">the element to write both ways</param>
    private static void AssertSame(OpenDocumentWritable writable)
    {
        var fromModel = WithoutNamespaceDeclarations(XElement.Parse(ViaElementModel(writable)));
        var fromWriter = WithoutNamespaceDeclarations(XElement.Parse(ViaStreaming(writable)));

        Assert.IsTrue(
            XNode.DeepEquals(fromModel, fromWriter),
            "streamed XML must match the element model." + Environment.NewLine +
            "model:    " + fromModel.ToString(SaveOptions.DisableFormatting) + Environment.NewLine +
            "streamed: " + fromWriter.ToString(SaveOptions.DisableFormatting));
    }

    /// <summary>
    /// Drops namespace declarations, which XLinq models as attributes. Which prefix a declaration
    /// uses is a spelling choice, not content, and the two paths make it differently on a
    /// standalone element.
    /// </summary>
    /// <param name="element">the element to strip</param>
    /// <returns>the same element without namespace declaration attributes</returns>
    private static XElement WithoutNamespaceDeclarations(XElement element)
    {
        foreach (var node in element.DescendantsAndSelf())
        {
            node.Attributes().Where(a => a.IsNamespaceDeclaration).Remove();
        }
        return element;
    }

    [TestMethod]
    public void CoveredTableCellMatchesTest() => AssertSame(new OpenDocumentCoveredTableCell());

    [TestMethod]
    public void EmptyTableCellMatchesTest() => AssertSame(new OpenDocumentTableCell());

    [TestMethod]
    public void TextTableCellMatchesTest() => AssertSame(new OpenDocumentTableCell
    {
        StyleName = "ce1",
        ValueType = "string",
    });

    [TestMethod]
    public void NumericTableCellMatchesTest() => AssertSame(new OpenDocumentTableCell
    {
        StyleName = "ce2",
        ValueType = "float",
        Value = 1234.5678,
    });

    [TestMethod]
    public void RepeatedAndSpannedCellMatchesTest() => AssertSame(new OpenDocumentTableCell
    {
        NumberColumnsRepeated = 17,
        NumberColumnsSpanned = 3,
        NumberRowsSpanned = 2,
        Formula = "of:=SUM([.A1:.A9])",
        StyleName = "ce3",
    });

    [TestMethod]
    public void TableRowMatchesTest() => AssertSame(new OpenDocumentTableRow
    {
        StyleName = "ro1",
        NumberRowsRepeated = "4",
        TableCells =
        {
            new OpenDocumentTableCell { StyleName = "ce1", ValueType = "string" },
            new OpenDocumentTableCell { NumberColumnsRepeated = 9 },
        },
    });

    [TestMethod]
    public void ColumnMatchesTest() => AssertSame(new Column("co1")
    {
        DefaultCellStyleName = "ce1",
        NumberColumnsRepeated = "12",
    });

    [TestMethod]
    public void StyleMatchesTest() => AssertSame(new OpenDocumentStyle
    {
        Name = "auto_col_0",
        Family = DataTypes.StyleFamily.TableColumn,
        TableColumnProperties = new TableColumnProperties
        {
            BreakBefore = DataTypes.BreakValue.Auto,
            ColumnWidth = new DataTypes.Measurement(20, DataTypes.Unit.MM),
        },
    });

    [TestMethod]
    public void TableWithRowsAndColumnsMatchesTest()
    {
        var doc = new OpenDocumentSpreadsheet();
        var ag = new AutoGrid(doc, "T", 2, 3, "20mm");
        ag.WriteCell(0, 0, "text");
        ag.WriteCell(1, 0, 42.5);
        ag.WriteCell(2, 1, "more");

        AssertSame(ag);
    }
}
