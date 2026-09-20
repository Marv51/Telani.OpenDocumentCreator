using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
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

    /// <summary>
    /// Column styles by the properties they carry, so that reusing one does not need a scan of
    /// every style in the document.
    /// </summary>
    private readonly Dictionary<TableColumnProperties, string> tableColumnStyleIndex = [];

    /// <summary>
    /// How many styles the document held when <see cref="tableColumnStyleIndex"/> was last known to
    /// be complete. <see cref="Styles"/> is public and mutable, so a differing count means the index
    /// has to be rebuilt before it can be trusted to be exhaustive.
    /// </summary>
    private int styleCountWhenIndexed = -1;

    /// <summary>The suffix to try next when naming a generated column style.</summary>
    private int nextAutoColumnStyleNumber;

    /// <summary>
    /// Finds the column style carrying these properties, creating and registering it if the
    /// document does not have one yet.
    /// </summary>
    /// <param name="properties">the column properties the style has to carry</param>
    /// <returns>a style of family <see cref="StyleFamily.TableColumn"/> with those properties</returns>
    internal OpenDocumentStyle GetOrAddTableColumnStyle(TableColumnProperties properties)
    {
        if (styleCountWhenIndexed != Styles.Count)
        {
            RebuildTableColumnStyleIndex();
        }

        // The index can name a style that has since been replaced, so confirm the hit before
        // handing it back.
        if (tableColumnStyleIndex.TryGetValue(properties, out var name)
            && Styles.TryGetValue(name, out var found)
            && found.Family == StyleFamily.TableColumn
            && found.TableColumnProperties is not null
            && found.TableColumnProperties == properties)
        {
            return found;
        }

        var newStyle = new OpenDocumentStyle
        {
            Name = NextAutoColumnStyleName(),
            Family = StyleFamily.TableColumn,
            TableColumnProperties = properties,
        };
        Styles.Add(newStyle.Name, newStyle);
        tableColumnStyleIndex[properties] = newStyle.Name;
        styleCountWhenIndexed = Styles.Count;
        return newStyle;
    }

    /// <summary>
    /// Produces a free name for a generated column style. Counting styles rather than generated
    /// names used to be able to collide, either with a style a caller named "auto_col_N" itself or
    /// after a style was removed.
    /// </summary>
    /// <returns>a name no style in the document currently uses</returns>
    private string NextAutoColumnStyleName()
    {
        string name;
        do
        {
            name = "auto_col_" + nextAutoColumnStyleNumber++;
        }
        while (Styles.ContainsKey(name));
        return name;
    }

    private void RebuildTableColumnStyleIndex()
    {
        tableColumnStyleIndex.Clear();
        foreach (var style in Styles.Values)
        {
            if (style.Family == StyleFamily.TableColumn && style.TableColumnProperties is not null && style.Name is not null)
            {
                tableColumnStyleIndex[style.TableColumnProperties] = style.Name;
            }
        }
        styleCountWhenIndexed = Styles.Count;
    }

    /// <inheritdoc />
    public OpenDocumentStyle GetStyleByName(string s) => Styles[s] ?? throw new InvalidOperationException("Style not found");

    private OpenDocumentDocumentStyles CreateStyleFileRoot(string documentFont) => new()
    {
        FontFaceDecls = CreateFontFaceDecl(),
        Styles = CreateStyles(documentFont),
        AutomaticStyles = CreateAutomaticStyles(),
        MasterStyles = CreateMasterStyles(),
    };

    private XDocument CreateStyleFile(string documentFont) => CreateDocument(CreateStyleFileRoot(documentFont));

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

    /// <summary>
    /// The content file as the element model builds it. Exists so that tests can compare the
    /// streaming path against the path it replaced.
    /// </summary>
    /// <returns>content.xml as an element tree</returns>
    internal XDocument BuildContentFileForTesting() => CreateContentFile();

    /// <summary>The styles part as the element model builds it.</summary>
    /// <returns>styles.xml as an element tree</returns>
    internal XDocument BuildStyleFileForTesting() => CreateStyleFile("Calibri");

    /// <summary>The meta part as the element model builds it.</summary>
    /// <returns>meta.xml as an element tree</returns>
    internal XDocument BuildMetaFileForTesting() => MetaData.XmlMeta;

    /// <summary>The manifest as the element model builds it.</summary>
    /// <returns>manifest.xml as an element tree</returns>
    internal XDocument BuildManifestForTesting() => CreateManifest();

    /// <summary>
    /// Writes the document's content directly, without building it as an element tree first.
    /// </summary>
    /// <param name="writer">the writer to write to</param>
    /// <remarks>
    /// The default falls back to <see cref="CreateContent"/>, so a document type that has not been
    /// ported still saves correctly, just without the memory saving.
    /// </remarks>
    internal virtual void WriteContent(XmlWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer, nameof(writer));
        CreateContent().WriteTo(writer);
    }

    private OpenDocumentDocumentContent CreateStreamingContentRoot(string documentFont) => new()
    {
        FontFaceDecls = CreateFontFaceDecl(),
        AutomaticStyles = CreateAutomaticStyles(),
        Body = new OpenDocumentBody() { ContentWriter = WriteContent },
    };

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

    /// <summary>
    /// The prefix and URI to use for a namespace name, for writers that need the prefix spelled
    /// out. Mirrors <see cref="FindNamespace"/>, including its fallback to the style namespace, so
    /// that the prefix always matches the URI that would be chosen.
    /// </summary>
    /// <param name="name">the namespace name, or null for the style namespace</param>
    /// <returns>the prefix and the namespace URI</returns>
    internal static (string Prefix, string Uri) FindPrefixedNamespace(string? name)
    {
        if (name is not null && Namespaces.TryGetValue(name, out var theNamespace))
        {
            return (name, theNamespace.NamespaceName);
        }
        return ("style", Style.NamespaceName);
    }

    private OpenDocumentManifest CreateManifestRoot()
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

        return new OpenDocumentManifest
        {
            Version = "1.2",
            Entries = entries,
        };
    }

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

        var root = CreateManifestRoot();
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
        var metaFile = MetaData.XmlMeta;
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
                    CreateStyleFile(documentFont).Save(Path.Combine(dirName, "styles.xml"));
                    CreateContentFile().Save(Path.Combine(dirName, "content.xml"));
                    metaFile.Save(Path.Combine(dirName, "meta.xml"));
                    File.WriteAllText(Path.Combine(dirName, "mimetype"), mimeType);
                }
                var dirName_meta = Path.Combine(dirName, "META-INF");
                if (!Directory.Exists(dirName_meta))
                {
                    Directory.CreateDirectory(dirName_meta);
                    CreateManifest().Save(Path.Combine(dirName_meta, "manifest.xml"));
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
        AddStreamedXMLEntry(zip, "styles.xml", CreateStyleFileRoot(documentFont));
        AddStreamedXMLEntry(zip, "content.xml", CreateStreamingContentRoot(documentFont));

        // meta.xml is hand built as a small tree rather than from a writable, so there is nothing
        // to stream; it goes through the same entry writer only so that every part shares one set
        // of writer settings and one byte order mark.
        AddStreamedXMLEntry(zip, "meta.xml", metaFile);
        AddStreamedXMLEntry(zip, "META-INF/manifest.xml", CreateManifestRoot());
    }

    /// <summary>
    /// Writes one part straight through an XmlWriter, without building the whole part as an
    /// element tree first.
    /// </summary>
    private static void AddStreamedXMLEntry(ZipArchive zip, string path, XDocument document, CompressionLevel compression = CompressionLevel.Optimal)
        => WriteEntry(zip, path, compression, writer => document.Root?.WriteTo(writer));

    private static void AddStreamedXMLEntry(ZipArchive zip, string path, OpenDocumentWritable root, CompressionLevel compression = CompressionLevel.Optimal)
        => WriteEntry(zip, path, compression, root.WriteTo);

    private static void WriteEntry(ZipArchive zip, string path, CompressionLevel compression, Action<XmlWriter> writeRoot)
    {
        var newEntry = zip.CreateEntry(path, compression);
        using var archiveStream = newEntry.Open();

        // Byte for byte what XDocument.Save produced for these parts: the same declaration, UTF-8
        // with a byte order mark, and no indentation.
        //
        // The mark is written here rather than left to the writer. Whether XmlWriter emits one
        // depends on how it was handed the stream, and getting two of them produces a file no
        // parser will read, so the encoding is set to the variant that emits none and the mark is
        // written explicitly.
        var settings = new XmlWriterSettings
        {
            Indent = false,
            CloseOutput = false,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };

        var preamble = Encoding.UTF8.GetPreamble();
        archiveStream.Write(preamble, 0, preamble.Length);

        using (var writer = XmlWriter.Create(archiveStream, settings))
        {
            writer.WriteStartDocument(true);
            writeRoot(writer);
            writer.WriteEndDocument();
        }
        archiveStream.Flush();
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
