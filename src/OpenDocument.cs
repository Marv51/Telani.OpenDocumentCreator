using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Schema;
using OpenDocumentCreator.Styles;

namespace OpenDocumentCreator;

internal class SerializationHelper(Func<OpenDocumentWritable, object?> getter)
{
    public Func<OpenDocumentWritable, object?> Getter { get; } = getter;

    public OpenDocumentNameAttribute? OpenDocumentNameAttribute { get; set; }
}

/// <summary>
/// An abstract OpenDocument document. Inheriting classes will specify the exact type of the document.
/// </summary>
/// <param name="creatorName">The name of the author of this document.</param>
public abstract class OpenDocument(string creatorName = "") : IStyleLookup
{
    /// <summary>
    /// The meta data object that contains general info that will be embedded in this document.
    /// </summary>
    internal OpenDocumentMetaData MetaData { get; } = new OpenDocumentMetaData(creatorName, "telani.OpenDocumentCreator");

    /// <summary>
    /// The table xml-namespace.
    /// </summary>
    internal static readonly XNamespace Table = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:table:1.0");

    /// <summary>
    /// The office xml-namespace.
    /// </summary>
    internal static readonly XNamespace Office = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:office:1.0");

    /// <summary>
    /// The style xml-namespace.
    /// </summary>
    internal static readonly XNamespace Style = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:style:1.0");

    /// <summary>
    /// The text xml-namespace.
    /// </summary>
    internal static readonly XNamespace Text = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:text:1.0");

    /// <summary>
    /// The draw xml-namespace.
    /// </summary>
    internal static readonly XNamespace Draw = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:drawing:1.0");

    /// <summary>
    /// The fo xml-namespace.
    /// </summary>
    internal static readonly XNamespace Fo = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0");

    /// <summary>
    /// The xlink xml-namespace.
    /// </summary>
    internal static readonly XNamespace XLink = XNamespace.Get("http://www.w3.org/1999/xlink");

    /// <summary>
    /// The dc xml-namespace.
    /// </summary>
    internal static readonly XNamespace Dc = XNamespace.Get("http://purl.org/dc/elements/1.1/");

    /// <summary>
    /// The number xml-namespace.
    /// </summary>
    internal static readonly XNamespace Number = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:datastyle:1.0");

    /// <summary>
    /// The svg xml-namespace.
    /// </summary>
    internal static readonly XNamespace Svg = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0");

    /// <summary>
    /// The of xml-namespace.
    /// </summary>
    internal static readonly XNamespace Of = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:of:1.2");

    /// <summary>
    /// The manifest xml-namespace.
    /// </summary>
    internal static readonly XNamespace Manifest = XNamespace.Get("urn:oasis:names:tc:opendocument:xmlns:manifest:1.0");

    internal static readonly ReadOnlyDictionary<string, XNamespace> Namespaces = new(new Dictionary<string, XNamespace>
        {
            { "table", Table },
            { "office", Office },
            { "style", Style },
            { "text", Text },
            { "draw", Draw },
            { "fo", Fo },
            { "xlink", XLink },
            { "dc", Dc },
            { "number", Number },
            { "svg", Svg },
            { "of", Of },
            { "manifest", Manifest },
        });

    /// <summary>
    /// All the styles of the document.
    /// </summary>
    /// <value>Dictionary mapping style name to style</value>
    public Dictionary<string, OpenDocumentStyle> Styles { get; } = [];

    /// <inheritdoc />
    public OpenDocumentStyle GetStyleByName(string s) => Styles[s] ?? throw new InvalidOperationException("Style not found");

    private XDocument CreateStyleFile(string documentFont) => CreateDocument(new OpenDocumentDocumentStyles()
    {
        FontFaceDecls = CreateFontFaceDecl(),
        Styles = CreateStyles(documentFont),
        AutomaticStyles = CreateAutomaticStyles(),
        MasterStyles = CreateMasterStyles(),
    });

    private static OpenDocumentMasterStyles CreateMasterStyles() => new()
    {
        MasterPage = new OpenDocumentMasterPage
        {
            Name = "mp1",
            PageLayoutName = "pm1",
            Header = new OpenDocumentHeader(),
            HeaderLeft = new OpenDocumentHeaderLeft()
            {
                Display = "false",
            },
            Footer = new OpenDocumentFooter(),
            FooterLeft = new OpenDocumentFooterLeft
            {
                Display = "false",
            },
        },
    };

    /*
    private static OpenDocumentAutomaticStyles CreateAutomaticStylesForStyles()
    {
        var pageLayout = new OpenDocumentPageLayout()
        {
            Name = "pm1",
            PageLayoutProperties = new OpenDocumentPageLayoutProperties()
            {
                MarginTop = new Measurement(0.3m, Unit.Inch), //remember: 0.3m is a decimal literal
                MarginBottom = new Measurement(0.3m, Unit.Inch),
                MarginLeft = new Measurement(0.7m, Unit.Inch),
                MarginRight = new Measurement(0.7m, Unit.Inch),
                TableCentering = TableCentering.None,
                Print = "objects charts drawings"
            }
        };
        var header = new OpenDocumentHeaderFooterProperties()
        {
            MinHeight = new Measurement(0.487401575m, Unit.Inch),
            MarginLeft = new Measurement(0.7m, Unit.Inch),
            MarginRight = new Measurement(0.7m, Unit.Inch),
            MarginBottom = new Measurement(0m, Unit.Inch)
        };
        var h = new OpenDocumentHeaderStyle();
        h.Content.Add(header);
        pageLayout.HeaderStyle = h;

        var footer = new OpenDocumentFooterStyle();
        footer.Content.Add(new OpenDocumentHeaderFooterProperties()
        {
            MinHeight = new Measurement(0.487401575m, Unit.Inch),
            MarginLeft = new Measurement(0.7m, Unit.Inch),
            MarginRight = new Measurement(0.7m, Unit.Inch),
            MarginBottom = new Measurement(0m, Unit.Inch)
        });
        pageLayout.FooterStyle = footer;
        var autoStyles = new OpenDocumentAutomaticStyles
        {
            PageLayout = pageLayout
        };
        return autoStyles;
    }
    */
    private static OpenDocumentStyles CreateStyles(string documentFont)
    {
        var stylesElem = new OpenDocumentStyles();

        stylesElem.NumberStyles.Add(new OpenDocumentNumberStyle()
        {
            Name = "N0",
            Number = new OpenDocumentNumber()
            {
                MinIntegerDigits = "1",
            },
        });

        stylesElem.Styles.Add(new OpenDocumentStyle()
        {
            Name = "Default",
            Family = StyleFamily.TableCell,
            DataStyleName = "N0",
            TableCellProperties = new TableCellProperties()
            {
                VerticalAlign = VerticalAlign.Automatic,
                BackgroundColor = new Color(transparent: true),
            },
            TextProperties = new TextProperties()
            {
                Color = new Color(0, 0, 0),
                FontSize = new Measurement(11, Unit.PT),
                FontSizeAsian = new Measurement(11, Unit.PT),
                FontSizeComplex = new Measurement(11, Unit.PT),
                FontName = documentFont,
                FontNameAsian = documentFont,
                FontNameComplex = documentFont,
            },
        });
        stylesElem.Styles.Add(new OpenDocumentStyle()
        {
            Name = "Default",
            Family = StyleFamily.TableCell,
            DataStyleName = "N0",
            TableCellProperties = new TableCellProperties
            {
                VerticalAlign = VerticalAlign.Automatic,
                BackgroundColor = new Color(transparent: true),
            },
            TextProperties = new TextProperties()
            {
                Color = new Color(0, 0, 0),
                FontName = documentFont,
                FontNameAsian = documentFont,
                FontNameComplex = documentFont,
                FontSize = new Measurement(11, Unit.PT),
                FontSizeAsian = new Measurement(11, Unit.PT),
                FontSizeComplex = new Measurement(11, Unit.PT),
            },
        });

        stylesElem.Styles.Add(new OpenDocumentStyle()
        {
            Name = "Link",
            Family = StyleFamily.TableCell,
            DataStyleName = "N0",
            TextProperties = new TextProperties()
            {
                Color = new OpenDocumentCreator.DataTypes.Color(0x05, 0x63, 0xC1),
                TextUnderlineStyle = LineStyle.Solid,
                TextUnderlineType = LineType.SingleLine,
            },
        });

        return stylesElem;
    }

    private XDocument CreateContentFile() => CreateDocument(new OpenDocumentDocumentContent
    {
        FontFaceDecls = CreateFontFaceDecl(),
        AutomaticStyles = CreateAutomaticStyles(),
        Body = new OpenDocumentBody()
        {
            Content = CreateContent(),
        },
    });

    /// <summary>
    /// Create the content.
    /// </summary>
    /// <returns>XML node representing the content</returns>
    protected abstract XElement CreateContent();

    private static OpenDocumentFontFaceDecls CreateFontFaceDecl()
    {
        var decl = new OpenDocumentFontFaceDecls();
        decl.Content.Add(new OpenDocumentFontFace()
        {
            Name = "Calibri",
            FontFamily = "Calibri",
        });

        decl.Content.Add(new OpenDocumentFontFace()
        {
            Name = "Arial Narrow",
            FontFamily = "Arial Narrow",
        });

        return decl;
    }

    private OpenDocumentAutomaticStyles CreateAutomaticStyles()
    {
        var autostyle = new OpenDocumentAutomaticStyles();
        foreach (var (key, s) in Styles)
        {
            Debug.Assert(key == s.Name);
            autostyle.Styles.Add(s);
        }
        return autostyle;
    }

    private static XDocument CreateDocument(OpenDocumentWritable s)
        => new(new XDeclaration("1.0", "utf-8", "yes"), GetElementFor(s));

    internal static ConcurrentDictionary<string, IList<SerializationHelper>> TypeCache { get; } = new();

    internal static XElement GetElementFor(OpenDocumentWritable s)
    {
        ArgumentNullException.ThrowIfNull(s, nameof(s));

        return s.GetElement();
    }

    internal static XNamespace FindNamespace(string name)
        => Namespaces.TryGetValue(name, out var theNamespace) ? theNamespace : Style;

    private XDocument CreateManifest()
    {
        var entries = new List<ManifestEntry>
                {
                    new() { FullPath = "/", MediaTyp = GetMimeType() },
                    new() { FullPath = "styles.xml", MediaTyp = "text/xml" },
                    new() { FullPath = "content.xml", MediaTyp = "text/xml" },
                    new() { FullPath = "meta.xml", MediaTyp = "text/xml" },
                };
        foreach (var (path, _) in resources)
        {
            entries.Add(new ManifestEntry() { FullPath = path, MediaTyp = "image/png" });
        }

        var root = new OpenDocumentManifest
        {
            Version = "1.2",
            Entries = entries,
        };
        var manifestDoc = CreateDocument(root);

        /*#if DEBUG
            var location = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            XmlSchemaSet schemas = new XmlSchemaSet();
            schemas.Add(manifest.NamespaceName, Path.Combine(location, @"Schemas\manifest.xsd"));

            string msg = "";
            manifestDoc.Validate(schemas, (o, e) => {
                msg += e.Message + Environment.NewLine;
            });
            if (msg.Length == 0)
            {
                throw new InvalidDataException("Manifest is not valid" + msg);
            }
        #endif*/
        return manifestDoc;
    }

    /// <summary>
    /// Finalize and save the document to a file at <paramref name="path"/>
    /// </summary>
    /// <seealso cref="Save(Stream, bool, string, bool)"/>
    /// <returns>Task to await the process</returns>
    /// <param name="path">The path to save the file to</param>
    /// <param name="unzip">Create unzipped</param>
    /// <param name="documentFont">The main font used in the document</param>
    public async Task Save(string path, bool unzip = false, string documentFont = "Calibri")
    {
        using var fileToSave = new FileStream(path, FileMode.Create);
        await Save(fileToSave, unzip, documentFont);
    }

    private readonly HashSet<(string Path, byte[] File)> resources = [];

    /// <summary>
    /// Add an image to the document. Prepares the image for use in the document.
    /// </summary>
    /// <param name="s">the image</param>
    /// <param name="filename">the name of the document</param>
    /// <returns>the path that can be used inside the document to refer to this image</returns>
    /// <exception cref="System.ArgumentNullException"></exception>
    public string AddImageResource(byte[] s, string filename)
    {
        ArgumentNullException.ThrowIfNull(filename, nameof(filename));

        // Optimization potential ahead:
        var existing = resources.Where(a => Enumerable.SequenceEqual(a.File, s));
        if (existing is not null && existing.Any())
        {
            return existing.First().Path;
        }

        var parts = filename.Split('.');
        var path = "media/image" + resources.Count + "." + parts[^1];
        resources.Add((path, s));
        return path;
    }

    /// <summary>
    /// Finalize and save the document to the <paramref name="fileToSave"/>
    /// </summary>
    /// <seealso cref="Save(string, bool, string)"/>
    /// <param name="fileToSave">the stream to save to</param>
    /// <param name="unzip">output unzipped </param>
    /// <param name="documentFont">the font used for the document</param>
    /// <param name="leaveOpen">true to leave <paramref name="fileToSave"/> open once the document
    /// has been written, so that the caller can keep using it. Defaults to false, which closes the
    /// stream. Note that the writing happens on a thread pool thread, so the returned task has to be
    /// awaited before the stream is touched either way.</param>
    /// <returns>Task to await the process</returns>
    public Task Save(Stream fileToSave, bool unzip = false, string documentFont = "Calibri", bool leaveOpen = false)
    {
        return Task.Run(() =>
        {
            ValidationBeforeSave();

            try
            {
                WriteZipFile(fileToSave, unzip, documentFont, leaveOpen);
            }
            catch (IOException ex)
            {
                Trace.TraceError("Could not write spreadsheet to stream! Message: {0} Stacktrace: {1}", ex.Message, ex.StackTrace);
                throw;
            }
        });
    }

    /// <summary>
    /// Validate document before saving.
    /// </summary>
    protected abstract void ValidationBeforeSave();

    private void WriteZipFile(Stream fileToSave, bool unzip, string documentFont, bool leaveOpen)
    {
        var styleFile = CreateStyleFile(documentFont);
        var contentFile = CreateContentFile();
        var metaFile = MetaData.XmlMeta;
        var manifestFile = CreateManifest();
        var mimeType = GetMimeType();
        if (unzip)
        {
            if (fileToSave is FileStream fileStream)
            {
                var dirName = fileStream.Name;
                dirName = dirName[..dirName.LastIndexOf(".ods", StringComparison.OrdinalIgnoreCase)];

                if (!Directory.Exists(dirName))
                {
                    Directory.CreateDirectory(dirName);
                    styleFile.Save(Path.Combine(dirName, "styles.xml"));
                    contentFile.Save(Path.Combine(dirName, "content.xml"));
                    metaFile.Save(Path.Combine(dirName, "meta.xml"));
                    File.WriteAllText(Path.Combine(dirName, "mimetype"), mimeType);
                }
                var dirName_meta = Path.Combine(dirName, "META-INF");
                if (!Directory.Exists(dirName_meta))
                {
                    Directory.CreateDirectory(dirName_meta);
                    manifestFile.Save(Path.Combine(dirName_meta, "manifest.xml"));
                }
                foreach (var (path, file) in resources)
                {
                    var dirName_img = Path.Combine(dirName, path[..path.LastIndexOf('/')]);
                    if (!Directory.Exists(dirName_img))
                    {
                        Directory.CreateDirectory(dirName_img);
                        File.WriteAllBytes(Path.Combine(dirName, path), file);
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("Cant convert to filestream");
            }
        }
        using var zip = new ZipArchive(fileToSave, ZipArchiveMode.Create, leaveOpen);
        AddEntry(zip, "mimetype", mimeType, CompressionLevel.NoCompression);
        foreach (var (path, file) in resources)
        {
            AddBinaryEntry(zip, path, file, path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ? CompressionLevel.Optimal : CompressionLevel.NoCompression);
        }
        AddXMLEntry(zip, "styles.xml", styleFile);
        AddXMLEntry(zip, "content.xml", contentFile);
        AddXMLEntry(zip, "meta.xml", metaFile);
        AddXMLEntry(zip, "META-INF/manifest.xml", manifestFile);
    }

    private static void AddXMLEntry(ZipArchive zip, string path, XDocument content, CompressionLevel compression = CompressionLevel.Optimal)
    {
        var newEntry = zip.CreateEntry(path, compression);
        using var archiveStream = newEntry.Open();

        // Indentation is the default for XDocument.Save, and it is pure overhead here: the
        // indenting whitespace sits between elements in element-only content, which ODF readers
        // ignore. Writing it made content.xml about a quarter larger.
        content.Save(archiveStream, SaveOptions.DisableFormatting);
        archiveStream.Flush();
    }

    private static void AddBinaryEntry(ZipArchive zip, string path, byte[] content, CompressionLevel compression)
    {
        var newEntry = zip.CreateEntry(path, compression);
        using var entryStream = newEntry.Open();
        entryStream.Write(content, 0, content.Length);
    }

    private static void AddEntry(ZipArchive zip, string path, string content, CompressionLevel compression)
    {
        var utf8WithoutBom = new System.Text.UTF8Encoding(false);
        var newEntry = zip.CreateEntry(path, compression);
        using var writer = new StreamWriter(newEntry.Open(), utf8WithoutBom);
        writer.Write(content);
        writer.Flush();
    }

    /// <summary>
    /// Get the MimeType of the document
    /// </summary>
    /// <returns>the mimetype</returns>
    protected abstract string GetMimeType();
}
