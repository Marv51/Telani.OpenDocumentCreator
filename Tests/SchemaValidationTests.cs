using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace OpenDocumentCreator.Tests;

/// <summary>
/// Validates the parts of a saved document that can be validated.
///
/// META-INF/manifest.xml is checked against the OASIS schema shipped in src/Schemas, which is
/// authoritative. The other parts cannot be: OpenDocument defines content.xml and styles.xml in
/// RELAX NG, and .NET validates XML Schema only, so there is nothing in the framework that can
/// read the official grammar. Those parts are instead checked for the structural rules the
/// library is responsible for, which is weaker than a schema and is labelled as such.
/// </summary>
[TestClass]
public sealed class SchemaValidationTests
{
    private const string ManifestNs = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";
    private static readonly XNamespace TableNs = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:table:1.0");
    private static readonly XNamespace OfficeNs = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:office:1.0");
    private static readonly XNamespace TextNs = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:text:1.0");

    private static OpenDocumentSpreadsheet BuildDocument()
    {
        var doc = new OpenDocumentSpreadsheet("Validation");
        var style = new Styles.OpenDocumentStyle { Name = "ce_x", Family = DataTypes.StyleFamily.TableCell };
        doc.Styles.Add(style.Name, style);

        var ag = new AutoGrid(doc, "Sheet", 4, 5, "20mm");
        doc.Tables.Add(ag);
        ag.WriteCell(0, 0, "text", style);
        ag.WriteCell(1, 0, 42.5, style);
        ag.WriteCell(2, 0, "two  spaces", style);
        ag.WriteCell(3, 0, new Uri("https://example.invalid/x"), style);
        ag.WriteCell(0, 1, new OpenDocumentCell("res") { Formula = "of:=A1" }, style);
        ag.WriteCell(0, 2, "spans", style);
        ag.SetCellSpan(0, 2, 2, 2);
        doc.AddImageResource([0x89, 0x50, 0x4E, 0x47], "pic.png");
        return doc;
    }

    private static async Task<Dictionary<string, string>> SaveParts()
    {
        var doc = BuildDocument();
        using MemoryStream mem = new();
        await doc.Save(mem, leaveOpen: true);

        mem.Position = 0;
        using var zip = new ZipArchive(mem, ZipArchiveMode.Read);

        var parts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in new[] { "content.xml", "styles.xml", "meta.xml", "META-INF/manifest.xml" })
        {
            using var reader = new StreamReader(zip.GetEntry(name)!.Open());
            parts[name] = (await reader.ReadToEndAsync()).TrimStart('﻿');
        }
        return parts;
    }

    [TestMethod]
    public async Task ManifestValidatesAgainstTheOasisSchemaTest()
    {
        var schemaPath = Path.Combine("Schemas", "manifest.xsd");
        Assert.IsTrue(File.Exists(schemaPath), "the OASIS manifest schema is missing from the test output: " + Path.GetFullPath(schemaPath));

        var schemas = new XmlSchemaSet();
        schemas.Add(ManifestNs, schemaPath);

        var parts = await SaveParts();
        var problems = new List<string>();

        XDocument.Parse(parts["META-INF/manifest.xml"])
            .Validate(schemas, (_, e) => problems.Add(e.Severity + ": " + e.Message));

        Assert.IsEmpty(problems, "manifest.xml does not satisfy the OASIS schema:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public async Task EveryPartIsWellFormedTest()
    {
        var problems = new List<string>();

        foreach (var (name, xml) in await SaveParts())
        {
            try
            {
                XDocument.Parse(xml);
            }
            catch (XmlException ex)
            {
                problems.Add(name + ": " + ex.Message);
            }
        }

        Assert.IsEmpty(problems, "a saved part is not well formed XML:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    [TestMethod]
    public async Task EveryPrefixUsedIsDeclaredTest()
    {
        // A writer that uses a prefix it never declared produces a file that parses only because
        // the reader is lenient. Resolving every name forces the question.
        foreach (var (name, xml) in await SaveParts())
        {
            var root = XDocument.Parse(xml).Root!;
            foreach (var element in root.DescendantsAndSelf())
            {
                Assert.IsNotEmpty(element.Name.NamespaceName, name + ": element " + element.Name.LocalName + " is in no namespace");

                foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
                {
                    Assert.IsNotEmpty(
                        attribute.Name.NamespaceName,
                        name + ": attribute " + attribute.Name.LocalName + " on " + element.Name.LocalName + " is in no namespace");
                }
            }
        }
    }

    /// <summary>
    /// Structural rules for content.xml that the library is responsible for. Not a schema check.
    /// </summary>
    [TestMethod]
    public async Task ContentFollowsTheStructuralRulesTest()
    {
        var content = XDocument.Parse((await SaveParts())["content.xml"]);
        var problems = new List<string>();

        foreach (var table in content.Descendants(TableNs + "table"))
        {
            if (table.Attribute(TableNs + "name") is null)
            {
                problems.Add("a table has no table:name");
            }

            foreach (var column in table.Elements(TableNs + "table-column"))
            {
                CheckPositiveCount(column, TableNs + "number-columns-repeated", problems);
            }

            foreach (var row in table.Elements(TableNs + "table-row"))
            {
                CheckPositiveCount(row, TableNs + "number-rows-repeated", problems);

                foreach (var cell in row.Elements(TableNs + "table-cell"))
                {
                    CheckPositiveCount(cell, TableNs + "number-columns-repeated", problems);
                    CheckPositiveCount(cell, TableNs + "number-columns-spanned", problems);
                    CheckPositiveCount(cell, TableNs + "number-rows-spanned", problems);

                    // office:value-type="float" promises an office:value beside it
                    if (cell.Attribute(OfficeNs + "value-type")?.Value == "float"
                        && cell.Attribute(OfficeNs + "value") is null)
                    {
                        problems.Add("a float cell has no office:value");
                    }
                }
            }
        }

        foreach (var spaceRun in content.Descendants(TextNs + "s"))
        {
            CheckPositiveCount(spaceRun, TextNs + "c", problems);
        }

        Assert.IsEmpty(problems, "content.xml breaks structural rules:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
    }

    private static void CheckPositiveCount(XElement element, XName attributeName, List<string> problems)
    {
        var attribute = element.Attribute(attributeName);
        if (attribute is null)
        {
            return;
        }

        if (!int.TryParse(attribute.Value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var count))
        {
            problems.Add(element.Name.LocalName + "/" + attributeName.LocalName + " is not a number: '" + attribute.Value + "'");
        }
        else if (count < 1)
        {
            problems.Add(element.Name.LocalName + "/" + attributeName.LocalName + " is " + count + ", which the format does not allow");
        }
    }
}
