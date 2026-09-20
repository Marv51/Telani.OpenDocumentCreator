using System.Globalization;

namespace OdcCompat;

/// <summary>
/// What to put in a cell. A recipe is a plain description, with no reference to either version of
/// the library, so the same one can be built twice - once against the released assembly and once
/// against this working copy - and the two results compared.
/// </summary>
internal enum CellKind
{
    Empty,
    Text,
    MultiLineText,
    TextWithSpaces,
    TextNeedingEscapes,
    Number,
    Formula,
    Link,
    Frame,
}

/// <summary>
/// How much of a frame to fill in. A bare frame carries a name and nothing else; the others add
/// the parts that serialize as nested elements.
/// </summary>
internal enum FrameKind
{
    Bare,
    Positioned,
    WithTextBox,
    WithImage,
}

/// <summary>
/// Which kind of document to write. The text document takes a different content writer, which
/// nothing else here reaches.
/// </summary>
internal enum DocumentKind
{
    Spreadsheet,
    Text,
}

/// <summary>
/// Mirrors the library's EmptyLineHandling without naming it, so this file stays free of library
/// types. <see cref="Unset"/> means leave the property alone.
/// </summary>
internal enum EmptyLineMode
{
    Unset,
    Collapse,
    Preserve,
    TrimEnds,
}

/// <summary>
/// One of the bulk writers. They take a different path through AutoGrid than WriteCell does, and
/// WriteRows in particular is enumerated rather than indexed.
/// </summary>
/// <summary>
/// How a cell write reaches the grid. A prebuilt cell replaces whatever was there; everything else
/// goes through the generic overload's type switch, which mutates the cell already in place. The
/// types are the ones that switch names - an int, oddly, sets the repeat count rather than a value.
/// </summary>
internal enum WriteAs
{
    Cell,
    Text,
    Number,
    SingleNumber,
    Link,
    Frame,
    RepeatCount,
    Unsupported,
}

internal enum BulkKind
{
    WriteColumn,
    WriteColumns,
    WriteRow,
    WriteRowsArray,
    WriteRowsEnumerable,
}

/// <summary>
/// How a row passed to one of the row writers is put together. The released version offers
/// several ways to fill a row and they do not all end in the same place.
/// </summary>
internal enum RowBuild
{
    AddCell,
    AddString,
    AddTuple,
    InsertCell,
    InsertCellWithStyle,
    InsertCells,
    TemplateString,
    AddThenReplace,
    AddThenRemove,
    AddThenClear,
}

/// <summary>
/// A measurement: a number, and an index into the library's Unit enum.
/// </summary>
internal sealed record MeasureRecipe(decimal Value, int Unit);

/// <summary>
/// A colour, or the transparent one.
/// </summary>
internal sealed record ColorRecipe(byte Red, byte Green, byte Blue, bool Transparent);

/// <summary>
/// A border line: a width, an index into the library's LineStyle enum, and a colour.
/// </summary>
internal sealed record LineRecipe(MeasureRecipe Width, int Style, ColorRecipe Color);

/// <summary>
/// A style, described without naming a library type.
/// </summary>
/// <remarks>
/// Enum valued properties are carried as an index into the corresponding library enum, and null
/// means leave the property unset. Build turns an index into a member by position, so every member
/// of every one of those enums is reachable without this file having to name any of them.
/// </remarks>
/// <summary>
/// Which family a style belongs to. Mirrors the library's StyleFamily.
/// </summary>
internal enum StyleTarget
{
    TableCell,
    TableColumn,
    TableRow,
    Table,
    Paragraph,
    Graphic,
}

internal sealed record StyleRecipe(
    string Name,
    StyleTarget Family,
    string? ParentName,
    string? DataStyleName,
    string? MasterPageName,
    CellProps? Cell,
    ParagraphProps? Paragraph,
    TextProps? Text,
    ColumnProps? Column,
    RowProps? Row,
    TableProps? Table,
    GraphicProps? Graphic);

/// <summary>
/// The properties of a style, one class per properties element. They are classes with init only
/// members rather than positional records because there are a great many of them and most are
/// left unset in any one style; null means the property is not written.
/// </summary>
internal sealed class CellProps
{
    public LineRecipe? Border { get; init; }

    public LineRecipe? BorderBottom { get; init; }

    public LineRecipe? BorderLeft { get; init; }

    public LineRecipe? BorderRight { get; init; }

    public LineRecipe? BorderTop { get; init; }

    public LineRecipe? DiagonalTopLeftBottomRight { get; init; }

    public LineRecipe? DiagonalTopLeftBottomRightWidths { get; init; }

    public LineRecipe? DiagonalBottomLeftTopRight { get; init; }

    public LineRecipe? DiagonalBottomLeftTopRightWidths { get; init; }

    public ColorRecipe? BackgroundColor { get; init; }

    public int? VerticalAlign { get; init; }

    public int? WrapOption { get; init; }

    public int? TextAlignSource { get; init; }

    public int? CellProtect { get; init; }

    public int? RotationAlign { get; init; }

    public int? Direction { get; init; }

    public int? WritingMode { get; init; }

    public int? ShrinkToFit { get; init; }

    public int? PrintContent { get; init; }

    public int? RepeatContent { get; init; }

    public string? RotationAngle { get; init; }

    public string? Padding { get; init; }

    public string? PaddingBottom { get; init; }

    public string? PaddingLeft { get; init; }

    public string? PaddingRight { get; init; }

    public string? PaddingTop { get; init; }

    public string? BorderLineWidth { get; init; }

    public string? BorderLineWidthBottom { get; init; }

    public string? BorderLineWidthTop { get; init; }

    public string? BorderLineWidthLeft { get; init; }

    public string? BorderLineWidthRight { get; init; }

    public string? DecimalPlaces { get; init; }

    public string? GlyphOrientationVertical { get; init; }

    public string? Shadow { get; init; }
}

internal sealed class ParagraphProps
{
    public int? TextAlign { get; init; }

    public MeasureRecipe? MarginLeft { get; init; }

    public string? LineBreak { get; init; }
}

internal sealed class TextProps
{
    public int? FontWeight { get; init; }

    public int? FontStyle { get; init; }

    public int? UnderlineStyle { get; init; }

    public int? UnderlineType { get; init; }

    public MeasureRecipe? FontSize { get; init; }

    public MeasureRecipe? FontSizeAsian { get; init; }

    public MeasureRecipe? FontSizeComplex { get; init; }

    public ColorRecipe? Color { get; init; }

    public ColorRecipe? BackgroundColor { get; init; }

    public string? FontFamily { get; init; }

    public string? FontName { get; init; }

    public string? FontNameAsian { get; init; }

    public string? FontNameComplex { get; init; }

    public string? FontVariant { get; init; }

    public string? FontCharset { get; init; }

    public string? Language { get; init; }

    public string? Country { get; init; }

    public string? Script { get; init; }

    public string? LetterSpacing { get; init; }

    public string? TextTransform { get; init; }

    public string? TextShadow { get; init; }

    public string? Hyphenate { get; init; }

    public string? HyphenationPushCharCount { get; init; }

    public string? HyphenationRemainCharCount { get; init; }

    public string? CountryAsian { get; init; }

    public string? CountryComplex { get; init; }

    public string? FontCharsetAsian { get; init; }

    public string? FontCharsetComplex { get; init; }

    public string? Display { get; init; }

    public string? Condition { get; init; }
}

internal sealed class ColumnProps
{
    public MeasureRecipe? ColumnWidth { get; init; }

    public int? UseOptimal { get; init; }

    public int? BreakBefore { get; init; }

    public int? BreakAfter { get; init; }

    public string? RelativeColumnWidth { get; init; }
}

internal sealed class RowProps
{
    public MeasureRecipe? RowHeight { get; init; }

    public MeasureRecipe? MinRowHeight { get; init; }

    public int? UseOptimal { get; init; }

    public int? BreakBefore { get; init; }

    public int? BreakAfter { get; init; }

    public ColorRecipe? BackgroundColor { get; init; }

    public string? KeepTogether { get; init; }
}

internal sealed class TableProps
{
    public int? WritingMode { get; init; }

    public int? Display { get; init; }
}

internal sealed class GraphicProps
{
    public int? Fill { get; init; }

    public int? Stroke { get; init; }

    public ColorRecipe? FillColor { get; init; }

    public ColorRecipe? StrokeColor { get; init; }

    public MeasureRecipe? StrokeWidth { get; init; }

    public MeasureRecipe? PaddingTop { get; init; }

    public MeasureRecipe? PaddingLeft { get; init; }

    public string? Opacity { get; init; }

    public string? StrokeOpacity { get; init; }

    public string? StrokeLineCap { get; init; }

    public string? StrokeLineJoin { get; init; }

    public string? AutoGrowHeight { get; init; }

    public string? ColorMode { get; init; }

    public string? Contrast { get; init; }

    public string? Gamma { get; init; }

    public string? Luminance { get; init; }

    public string? Mirror { get; init; }

    public string? ImageOpacity { get; init; }

    public string? TextareaHorizontalAlign { get; init; }

    public string? TextareaVerticalAlign { get; init; }

    public string? Clip { get; init; }

    public string? Red { get; init; }

    public string? Green { get; init; }

    public string? Blue { get; init; }
}

/// <summary>
/// One write into the grid. <c>Extras</c> is a small bit set laid over whatever the kind already
/// put in the cell: bit 0 adds a formula, bit 1 a link, bit 2 a frame, bit 3 a text content. The
/// cell writer decides between those by precedence rather than writing all of them, so the
/// combinations are what pins that order down.
/// </summary>
internal sealed record CellStep(
    int X,
    int Y,
    CellKind Kind,
    string Text,
    double Number,
    int StyleIndex,
    EmptyLineMode EmptyLines,
    int Repeat,
    FrameRecipe Frame,
    int ColumnsSpanned,
    int RowsSpanned,
    bool IsCovered,
    int Extras,
    WriteAs Write);

/// <summary>
/// A frame, whether it hangs off a cell or off the table's shapes.
/// </summary>
internal sealed record FrameRecipe(
    FrameKind Kind,
    string Name,
    string? DrawingId,
    string? StyleName,
    string? TextStyleName,
    string? ParagraphStyle,
    MeasureRecipe? X,
    MeasureRecipe? Y,
    MeasureRecipe? Width,
    MeasureRecipe? Height,
    int? ZIndex,
    string? RelWidth,
    string? RelHeight,
    string Text);

/// <summary>
/// A use of AutoColumnProcessor, which is the other way to put columns on a table: it turns widths
/// into generated column styles itself, sharing one style between columns of equal width.
/// </summary>
/// <param name="FromTemplate">parse a template string rather than asking for a count</param>
/// <param name="Template">the template, when parsing one</param>
/// <param name="Count">how many columns, when not</param>
/// <param name="Width">how wide they are, when not</param>
internal sealed record AutoColumnSpec(bool FromTemplate, string Template, int Count, string Width);

/// <summary>
/// A column added to the table by hand, rather than one AutoGrid made.
/// </summary>
internal sealed record ColumnRecipe(string? StyleName, string? DefaultCellStyleName, string Repeat, int? Visibility);

internal sealed record SpanStep(int X, int Y, int RowSpan, int ColumnSpan);

/// <summary>
/// A bulk write. <see cref="Values"/> is a list of rows of strings; the single row writers use the
/// first one and WriteColumn uses the first column of it.
/// </summary>
internal sealed record BulkStep(BulkKind Kind, RowBuild Build, int X, int Y, IReadOnlyList<IReadOnlyList<string>> Values, int StyleIndex);

/// <summary>
/// One table within a document.
/// </summary>
internal sealed record TableRecipe(
    string Name,
    string? StyleName,
    AutoColumnSpec? AutoColumns,
    IReadOnlyList<FrameRecipe> Shapes,
    IReadOnlyList<ColumnRecipe> ManualColumns,
    int Rows,
    int Columns,
    string DefaultWidth,
    IReadOnlyList<string> ColumnWidths,
    int ColumnWidthStart,
    IReadOnlyList<int> ColumnStyles,
    int ColumnStyleStart,
    IReadOnlyList<CellStep> Cells,
    IReadOnlyList<SpanStep> Spans,
    IReadOnlyList<int> StyledRows,
    IReadOnlyList<BulkStep> BulkWrites);

/// <summary>
/// A whole document, described without using either library.
/// </summary>
/// <summary>
/// A binary resource added to the document. The library dedupes them by content, so equal bytes
/// are deliberately likely.
/// </summary>
internal sealed record ImageResource(string FileName, int Content, int Length);

internal sealed record Recipe(
    int Seed,
    DocumentKind Kind,
    string DocumentFont,
    IReadOnlyList<ImageResource> Images,
    bool UnregisteredStyle,
    IReadOnlyList<StyleRecipe> CellStyles,
    IReadOnlyList<StyleRecipe> ColumnStyles,
    IReadOnlyList<StyleRecipe> RowStyles,
    IReadOnlyList<StyleRecipe> TableStyles,
    IReadOnlyList<StyleRecipe> GraphicStyles,
    IReadOnlyList<StyleRecipe> ExtraStyles,
    IReadOnlyList<TableRecipe> Tables)
{
    /// <summary>
    /// The text pool. Each entry is here because it takes a different route through the text
    /// writer: space runs at either end and in the middle, runs long enough that the text:c count
    /// goes past one digit, whitespace that is not a space and so must not be collapsed, characters
    /// outside the basic plane, and a string long enough to outgrow the first pooled buffer.
    /// </summary>
    private static readonly string[] Texts =
    [
        "value",
        "a longer piece of text",
        "Ümläute und ß",
        "trailing space ",
        "trailing run   ",          // a run at the very end, which is encoded asymmetrically
        "   leading run",           // a run at the very start
        "double  in  the  middle",
        "   ",                      // nothing but spaces
        "tab\tand\ttab",
        "x",
        string.Empty,
        "a run of fourteen              spaces",   // needs a two digit text:c
        " nbsp is not a space",
        "surrogates \U0001f600 \U0001f1e9\U0001f1ea end",
        "combining é and å",
        "\n",                       // nothing but a line break
        "\n\n\nleading blank lines",
        "trailing blank lines\n\n\n",
        "middle\n\n\nblank lines",
        "mixed \r\n line \n endings \r end",
        "right to left אבג end",
        LongText(),
    ];

    private static readonly decimal[] Measures =
        [0m, 0.001m, 0.74m, 1.5m, 9m, 14.5m, 20m, 32m, 40m, 72m, -3.25m, 1234.5678m];

    /// <summary>
    /// Table names the library does not take as given: the forbidden characters it rewrites, the
    /// reserved name it replaces, the empty one it fills in, one at the length limit, and a name
    /// repeated so that the unique name search has to do something.
    /// </summary>
    private static readonly string[] AwkwardNames =
    [
        "Plan [2026]",
        "a/b\\c",
        "why? *maybe*",
        "time: 10:30",
        "'quoted'",
        "History",
        "",
        "exactly thirty one characters ok",
        "Shared",
        "Shared",
        "Ümläute und ß",
        "with  two  spaces",
    ];

    /// <summary>
    /// Template strings for AutoColumnProcessor. A width is a number and a two character unit; the
    /// parser takes the unit as millimetres whatever it says, and tolerates spaces around the bar.
    /// </summary>
    private static readonly string[] Templates =
    [
        "|60mm|20mm|60mm|60mm|40mm|30mm|",
        " | 12mm | 12mm | 12mm | ",
        "|0mm|",
        "|1234.25mm|0.5mm|1234.25mm|",
        "|7mm|7mm|7mm|7mm|7mm|7mm|7mm|7mm|",
        "|20mm|",
    ];

    private static readonly string[] MasterPages = ["Default", "PageStyle_Sheet1", "Report"];

    private static readonly string[] LineWidths = ["0.05pt 0.05pt 0.05pt", "0.5mm 1mm 0.5mm", "thin"];

    private static readonly string[] Shadows = ["none", "#808080 0.18cm 0.18cm"];

    private static readonly string[] Variants = ["normal", "small-caps"];

    private static readonly string[] Charsets = ["x-symbol", "iso-8859-1"];

    private static readonly string[] Countries = ["DE", "AT", "none"];

    private static readonly string[] Scripts = ["Latn", "Arab"];

    private static readonly string[] Transforms = ["none", "uppercase", "lowercase", "capitalize"];

    private static readonly string[] Booleans = ["true", "false"];

    private static readonly string[] Displays = ["true", "none"];

    private static readonly string[] Conditions = ["none"];

    private static readonly string[] LineCaps = ["butt", "round", "square"];

    private static readonly string[] LineJoins = ["miter", "round", "bevel"];

    private static readonly string[] ColorModes = ["standard", "greyscale", "mono", "watermark"];

    private static readonly string[] Mirrors = ["none", "horizontal", "vertical"];

    /// <summary>
    /// File names for image resources. The library takes the extension by splitting on a dot and
    /// using the last part, so the ones without a dot and the one ending in a dot are the
    /// interesting entries.
    /// </summary>
    private static readonly string[] ImageNames =
    [
        "logo.png",
        "chart.jpeg",
        "a.b.c.gif",
        "no-extension",
        "trailing.",
        "Ümläute.png",
    ];

    private static readonly string[] Clips = ["auto", "rect(0cm, 0cm, 0cm, 0cm)"];

    private static readonly string[] Aligns = ["left", "center", "right", "justify"];

    private static readonly string[] DataStyles = ["N0", "N2", "N109"];

    private static readonly string[] Angles = ["0", "90", "270"];

    private static readonly string[] Paddings = ["0.097cm", "1mm", "0in", "2.5pt"];

    private static readonly string[] Counts = ["0", "2", "6"];

    private static readonly string[] Fonts = ["Calibri", "Liberation Sans", "A Font With  Spaces"];

    private static readonly string[] Languages = ["de", "en", "zxx"];

    private static readonly string[] RelativeWidths = ["1*", "8000*"];

    private static readonly string[] Opacities = ["0%", "50%", "100%"];

    private static readonly string[] Widths = ["11mm", "20mm", "35mm", "7mm", "120mm", "0mm", "1.5mm", "1234.25mm"];

    /// <summary>
    /// Builds a random recipe. The same seed always gives the same document, so a failure can be
    /// reproduced from the seed alone.
    /// </summary>
    /// <param name="seed">the seed to generate from</param>
    /// <returns>the recipe</returns>
    public static Recipe Generate(int seed)
    {
        var random = new Random(seed);

        var cellStyles = Styles(random, StyleTarget.TableCell, "ce", random.Next(0, 5));
        var columnStyles = Styles(random, StyleTarget.TableColumn, "co", random.Next(0, 4));
        var rowStyles = Styles(random, StyleTarget.TableRow, "ro", random.Next(1, 4));
        var tableStyles = Styles(random, StyleTarget.Table, "ta", random.Next(1, 3));
        var graphicStyles = Styles(random, StyleTarget.Graphic, "gr", random.Next(0, 3));

        var extra = random.Next(0, 2) == 0 ? Styles(random, StyleTarget.Paragraph, "P", 1) : [];

        // Now and then a spreadsheet with no tables at all, which the library fills in with one
        // of its own on the way out.
        var tables = new List<TableRecipe>();
        var tableCount = random.Next(0, 20) == 0 ? 0 : random.Next(1, 4);
        for (var i = 0; i < tableCount; i++)
        {
            tables.Add(GenerateTable(random, i, cellStyles.Count, columnStyles.Count, tableStyles.Count, graphicStyles.Count));
        }

        // A text document now and then. It has a content writer of its own, which nothing else
        // in the corpus reaches, and it ignores the tables.
        var kind = random.Next(0, 12) == 0 ? DocumentKind.Text : DocumentKind.Spreadsheet;

        var images = new List<ImageResource>();
        for (var i = 0; i < random.Next(0, 4); i++)
        {
            images.Add(new ImageResource(
                ImageNames[random.Next(ImageNames.Length)],
                random.Next(0, 3),
                random.Next(0, 4) == 0 ? 0 : random.Next(1, 200)));
        }

        return new Recipe(
            seed,
            kind,
            random.Next(0, 10) == 0 ? string.Empty : Fonts[random.Next(Fonts.Length)],
            images,
            random.Next(0, 8) == 0,
            cellStyles,
            columnStyles,
            rowStyles,
            tableStyles,
            graphicStyles,
            extra,
            tables);
    }

    /// <summary>
    /// Builds a run of styles of one family, each carrying the properties that family writes.
    /// Some inherit from the one before, so the parent chain is exercised too.
    /// </summary>
    /// <param name="random">the source of randomness</param>
    /// <param name="family">the family to build</param>
    /// <param name="prefix">the prefix for the generated names</param>
    /// <param name="count">how many to build</param>
    /// <returns>the styles</returns>
    private static List<StyleRecipe> Styles(Random random, StyleTarget family, string prefix, int count)
    {
        var styles = new List<StyleRecipe>();

        for (var i = 0; i < count; i++)
        {
            var parent = i > 0 && random.Next(0, 3) == 0 ? prefix + "_" + (i - 1) : null;

            styles.Add(new StyleRecipe(
                prefix + "_" + i,
                family,
                parent,
                random.Next(0, 3) == 0 ? DataStyles[random.Next(DataStyles.Length)] : null,
                random.Next(0, 4) == 0 ? MasterPages[random.Next(MasterPages.Length)] : null,
                family == StyleTarget.TableCell ? CellProperties(random) : null,
                family is StyleTarget.TableCell or StyleTarget.Paragraph ? ParagraphProperties(random) : null,
                family is StyleTarget.TableCell or StyleTarget.Paragraph ? TextProperties(random) : null,
                family == StyleTarget.TableColumn ? ColumnProperties(random) : null,
                family == StyleTarget.TableRow ? RowProperties(random) : null,
                family == StyleTarget.Table ? TableProperties(random) : null,
                family == StyleTarget.Graphic ? GraphicProperties(random) : null));
        }

        return styles;
    }

    private static CellProps CellProperties(Random random) => new()
    {
        Border = MaybeLine(random),
        BorderBottom = MaybeLine(random),
        BorderLeft = MaybeLine(random),
        BorderRight = MaybeLine(random),
        BorderTop = MaybeLine(random),
        DiagonalTopLeftBottomRight = MaybeLine(random),
        DiagonalTopLeftBottomRightWidths = MaybeLine(random),
        DiagonalBottomLeftTopRight = MaybeLine(random),
        DiagonalBottomLeftTopRightWidths = MaybeLine(random),
        BackgroundColor = MaybeColor(random),
        VerticalAlign = MaybeEnum(random),
        WrapOption = MaybeEnum(random),
        TextAlignSource = MaybeEnum(random),
        CellProtect = MaybeEnum(random),
        RotationAlign = MaybeEnum(random),
        Direction = MaybeEnum(random),
        WritingMode = MaybeEnum(random),
        ShrinkToFit = MaybeEnum(random),
        PrintContent = MaybeEnum(random),
        RepeatContent = MaybeEnum(random),
        RotationAngle = MaybeOne(random, Angles),
        Padding = MaybeOne(random, Paddings),
        PaddingBottom = MaybeOne(random, Paddings),
        PaddingLeft = MaybeOne(random, Paddings),
        PaddingRight = MaybeOne(random, Paddings),
        PaddingTop = MaybeOne(random, Paddings),
        BorderLineWidth = MaybeOne(random, LineWidths),
        BorderLineWidthBottom = MaybeOne(random, LineWidths),
        BorderLineWidthTop = MaybeOne(random, LineWidths),
        BorderLineWidthLeft = MaybeOne(random, LineWidths),
        BorderLineWidthRight = MaybeOne(random, LineWidths),
        DecimalPlaces = MaybeOne(random, Counts),
        GlyphOrientationVertical = MaybeOne(random, Angles),
        Shadow = MaybeOne(random, Shadows),
    };

    private static ParagraphProps ParagraphProperties(Random random) => new()
    {
        TextAlign = MaybeEnum(random),
        MarginLeft = MaybeMeasure(random),
        LineBreak = MaybeOne(random, Counts),
    };

    private static TextProps TextProperties(Random random) => new()
    {
        FontWeight = MaybeEnum(random),
        FontStyle = MaybeEnum(random),
        UnderlineStyle = MaybeEnum(random),
        UnderlineType = MaybeEnum(random),
        FontSize = MaybeMeasure(random),
        FontSizeAsian = MaybeMeasure(random),
        FontSizeComplex = MaybeMeasure(random),
        Color = MaybeColor(random),
        BackgroundColor = MaybeColor(random),
        FontFamily = MaybeOne(random, Fonts),
        FontName = MaybeOne(random, Fonts),
        FontNameAsian = MaybeOne(random, Fonts),
        FontNameComplex = MaybeOne(random, Fonts),
        FontVariant = MaybeOne(random, Variants),
        FontCharset = MaybeOne(random, Charsets),
        Language = MaybeOne(random, Languages),
        Country = MaybeOne(random, Countries),
        Script = MaybeOne(random, Scripts),
        LetterSpacing = MaybeOne(random, Paddings),
        TextTransform = MaybeOne(random, Transforms),
        TextShadow = MaybeOne(random, Shadows),
        Hyphenate = MaybeOne(random, Booleans),
        HyphenationPushCharCount = MaybeOne(random, Counts),
        HyphenationRemainCharCount = MaybeOne(random, Counts),
        CountryAsian = MaybeOne(random, Countries),
        CountryComplex = MaybeOne(random, Countries),
        FontCharsetAsian = MaybeOne(random, Charsets),
        FontCharsetComplex = MaybeOne(random, Charsets),
        Display = MaybeOne(random, Displays),
        Condition = MaybeOne(random, Conditions),
    };

    private static ColumnProps ColumnProperties(Random random) => new()
    {
        ColumnWidth = MaybeMeasure(random),
        UseOptimal = MaybeEnum(random),
        BreakBefore = MaybeEnum(random),
        BreakAfter = MaybeEnum(random),
        RelativeColumnWidth = MaybeOne(random, RelativeWidths),
    };

    private static RowProps RowProperties(Random random) => new()
    {
        RowHeight = MaybeMeasure(random),
        MinRowHeight = MaybeMeasure(random),
        UseOptimal = MaybeEnum(random),
        BreakBefore = MaybeEnum(random),
        BreakAfter = MaybeEnum(random),
        BackgroundColor = MaybeColor(random),
        KeepTogether = MaybeOne(random, Booleans),
    };

    private static TableProps TableProperties(Random random) => new()
    {
        WritingMode = MaybeEnum(random),
        Display = MaybeEnum(random),
    };

    private static GraphicProps GraphicProperties(Random random) => new()
    {
        Fill = MaybeEnum(random),
        Stroke = MaybeEnum(random),
        FillColor = MaybeColor(random),
        StrokeColor = MaybeColor(random),
        StrokeWidth = MaybeMeasure(random),
        PaddingTop = MaybeMeasure(random),
        PaddingLeft = MaybeMeasure(random),
        Opacity = MaybeOne(random, Opacities),
        StrokeOpacity = MaybeOne(random, Opacities),
        StrokeLineCap = MaybeOne(random, LineCaps),
        StrokeLineJoin = MaybeOne(random, LineJoins),
        AutoGrowHeight = MaybeOne(random, Booleans),
        ColorMode = MaybeOne(random, ColorModes),
        Contrast = MaybeOne(random, Opacities),
        Gamma = MaybeOne(random, Opacities),
        Luminance = MaybeOne(random, Opacities),
        Mirror = MaybeOne(random, Mirrors),
        ImageOpacity = MaybeOne(random, Opacities),
        TextareaHorizontalAlign = MaybeOne(random, Aligns),
        TextareaVerticalAlign = MaybeOne(random, Aligns),
        Clip = MaybeOne(random, Clips),
        Red = MaybeOne(random, Opacities),
        Green = MaybeOne(random, Opacities),
        Blue = MaybeOne(random, Opacities),
    };

    /// <summary>
    /// An index into a library enum, or null to leave that property unset. Build reduces the index
    /// modulo the number of members, so the whole of every one of those enums is reachable from a
    /// small range without this file naming any of them.
    /// </summary>
    /// <param name="random">the source of randomness</param>
    /// <returns>the index, or null</returns>
    private static int? MaybeEnum(Random random) => random.Next(0, 3) == 0 ? null : random.Next(0, 12);

    private static MeasureRecipe? MaybeMeasure(Random random)
        => random.Next(0, 3) == 0
            ? null
            : new MeasureRecipe(Measures[random.Next(Measures.Length)], random.Next(0, 12));

    private static ColorRecipe? MaybeColor(Random random)
    {
        if (random.Next(0, 3) == 0)
        {
            return null;
        }

        return random.Next(0, 8) == 0
            ? new ColorRecipe(0, 0, 0, true)
            : new ColorRecipe((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), false);
    }

    private static LineRecipe? MaybeLine(Random random)
        => random.Next(0, 3) == 0
            ? null
            : new LineRecipe(
                new MeasureRecipe(Measures[random.Next(Measures.Length)], random.Next(0, 12)),
                random.Next(0, 12),
                MaybeColor(random) ?? new ColorRecipe(0, 0, 0, false));

    private static string? MaybeOne(Random random, string[] pool)
        => random.Next(0, 3) == 0 ? null : pool[random.Next(pool.Length)];

    private static TableRecipe GenerateTable(
        Random random,
        int index,
        int cellStyleCount,
        int columnStyleCount,
        int tableStyleCount,
        int graphicStyleCount)
    {
        // Most tables stay small so that a run of a few thousand documents is quick. One in eight
        // is big enough to push the column padding and the repeat counts around.
        var large = random.Next(0, 8) == 0;
        var rows = large ? random.Next(20, 120) : random.Next(1, 16);
        var columns = large ? random.Next(10, 40) : random.Next(1, 12);

        var widthCount = random.Next(0, Math.Min(columns, 6) + 1);
        var widthStart = widthCount == 0 ? 0 : random.Next(0, columns - widthCount + 1);
        var widths = new List<string>();
        for (var i = 0; i < widthCount; i++)
        {
            // Repeats are deliberately likely: identical widths have to collapse onto one
            // generated column style.
            widths.Add(Widths[random.Next(Math.Min(Widths.Length, 3 + random.Next(0, 6)))]);
        }

        var columnStyleCountHere = columnStyleCount == 0 ? 0 : random.Next(0, Math.Min(columns, 4) + 1);
        var columnStyleStart = columnStyleCountHere == 0 ? 0 : random.Next(0, columns - columnStyleCountHere + 1);
        var columnStyles = new List<int>();
        for (var i = 0; i < columnStyleCountHere; i++)
        {
            columnStyles.Add(random.Next(0, columnStyleCount));
        }

        // Columns put on the table directly rather than through AutoGrid, carrying the parts of a
        // column that AutoGrid never sets.
        var manual = new List<ColumnRecipe>();
        for (var i = 0; i < random.Next(0, 3); i++)
        {
            manual.Add(new ColumnRecipe(
                columnStyleCount == 0 || random.Next(0, 2) == 0 ? null : "co_" + random.Next(0, columnStyleCount),
                cellStyleCount == 0 || random.Next(0, 2) == 0 ? null : "ce_" + random.Next(0, cellStyleCount),
                random.Next(0, 4) == 0 ? random.Next(2, 9).ToString(CultureInfo.InvariantCulture) : "1",
                random.Next(0, 2) == 0 ? null : random.Next(0, 6)));
        }

        // Templates are deliberately repetitive: equal widths have to collapse onto one
        // generated style, which is the part of this worth comparing.
        AutoColumnSpec? auto = random.Next(0, 3) == 0
            ? new AutoColumnSpec(
                random.Next(0, 2) == 0,
                Templates[random.Next(Templates.Length)],
                random.Next(1, 8),
                Widths[random.Next(Widths.Length)])
            : null;

        var shapes = new List<FrameRecipe>();
        for (var i = 0; i < random.Next(0, 3); i++)
        {
            shapes.Add(GenerateFrame(random, "sh" + index + "_" + i, graphicStyleCount));
        }

        var cellCap = large ? 800 : 300;
        var cells = new List<CellStep>();
        for (var i = 0; i < random.Next(0, Math.Min(rows * columns, cellCap) + 1); i++)
        {
            var kind = (CellKind)random.Next(Enum.GetValues<CellKind>().Length);
            var x = random.Next(0, columns);
            var y = random.Next(0, rows);

            cells.Add(new CellStep(
                x,
                y,
                kind,
                Texts[random.Next(Texts.Length)],
                Number(random),
                cellStyleCount == 0 ? -1 : random.Next(-1, cellStyleCount),
                (EmptyLineMode)random.Next(Enum.GetValues<EmptyLineMode>().Length),
                // A repeat of 1 is the ordinary case; the rest exercise number-columns-repeated.
                random.Next(0, 6) == 0 ? random.Next(1, 5) : 1,
                GenerateFrame(random, "f" + x + "_" + y, graphicStyleCount),
                // The real callers mostly span by setting these on the cell rather than by
                // calling SetCellSpan, so both routes are worth having.
                random.Next(0, 8) == 0 ? random.Next(1, 4) : 1,
                random.Next(0, 10) == 0 ? random.Next(1, 3) : 1,
                random.Next(0, 12) == 0,
                random.Next(0, 4) == 0 ? random.Next(1, 16) : 0,
                // Mostly a prebuilt cell, as a caller would; the rest walk the type switch.
                random.Next(0, 4) == 0
                    ? (WriteAs)random.Next(Enum.GetValues<WriteAs>().Length)
                    : WriteAs.Cell));
        }

        var spans = new List<SpanStep>();
        for (var i = 0; i < random.Next(0, 4); i++)
        {
            var x = random.Next(0, columns);
            var y = random.Next(0, rows);
            spans.Add(new SpanStep(x, y, random.Next(1, 4), random.Next(1, Math.Max(2, columns - x))));
        }

        var styledRows = new List<int>();
        for (var i = 0; i < random.Next(0, 4); i++)
        {
            styledRows.Add(random.Next(0, rows));
        }

        var bulk = new List<BulkStep>();
        for (var i = 0; i < random.Next(0, 4); i++)
        {
            bulk.Add(GenerateBulk(random, rows, columns, cellStyleCount));
        }

        return new TableRecipe(
            TableName(random, index),
            tableStyleCount == 0 || random.Next(0, 3) == 0 ? null : "ta_" + random.Next(0, tableStyleCount),
            auto,
            shapes,
            manual,
            rows,
            columns,
            Widths[random.Next(Widths.Length)],
            widths,
            widthStart,
            columnStyles,
            columnStyleStart,
            cells,
            spans,
            styledRows,
            bulk);
    }

    /// <summary>
    /// A table name. Most are ordinary, but the pool also holds names the library has to escape or
    /// replace, and a name shared between tables so that the unique name search has to run.
    /// </summary>
    /// <param name="random">the source of randomness</param>
    /// <param name="index">which table in the document this is</param>
    /// <returns>the name</returns>
    private static string TableName(Random random, int index)
        => random.Next(0, 3) == 0
            ? AwkwardNames[random.Next(AwkwardNames.Length)]
            : "Sheet" + index + "_" + random.Next(0, 100);

    private static FrameRecipe GenerateFrame(Random random, string name, int graphicStyleCount)
        => new(
            (FrameKind)random.Next(Enum.GetValues<FrameKind>().Length),
            name,
            random.Next(0, 3) == 0 ? null : "id_" + random.Next(0, 50),
            graphicStyleCount == 0 || random.Next(0, 2) == 0 ? null : "gr_" + random.Next(0, graphicStyleCount),
            random.Next(0, 3) == 0 ? null : "T1",
            random.Next(0, 3) == 0 ? null : "P_0",
            MaybeMeasure(random),
            MaybeMeasure(random),
            MaybeMeasure(random),
            MaybeMeasure(random),
            random.Next(0, 3) == 0 ? null : random.Next(0, 5),
            MaybeOne(random, Opacities),
            MaybeOne(random, Opacities),
            Texts[random.Next(Texts.Length)]);

    private static BulkStep GenerateBulk(Random random, int rows, int columns, int cellStyleCount)
    {
        var kind = (BulkKind)random.Next(Enum.GetValues<BulkKind>().Length);
        var build = (RowBuild)random.Next(Enum.GetValues<RowBuild>().Length);

        var x = random.Next(0, columns);
        var y = random.Next(0, rows);

        // Kept inside the grid: both versions reject overflowing writes, but they are not being
        // compared on how they word it.
        var height = Math.Max(1, random.Next(1, Math.Max(2, rows - y)));
        var width = Math.Max(1, random.Next(1, Math.Max(2, columns - x)));

        var values = new List<IReadOnlyList<string>>();
        for (var r = 0; r < height; r++)
        {
            var row = new List<string>();
            for (var c = 0; c < width; c++)
            {
                row.Add(Texts[random.Next(Texts.Length)]);
            }
            values.Add(row);
        }

        return new BulkStep(kind, build, x, y, values, cellStyleCount == 0 ? -1 : random.Next(-1, cellStyleCount));
    }

    /// <summary>
    /// A value for a numeric cell.
    /// </summary>
    /// <remarks>
    /// Deliberately finite and comfortably inside the range of a float. The released version stores
    /// cell values as a float, so a value outside that range is a place where the two versions are
    /// meant to differ, and the comparison is only interesting where they are meant to agree. The
    /// non finite values are left out for the same reason.
    /// </remarks>
    /// <param name="random">the source of randomness</param>
    /// <returns>the value</returns>
    private static double Number(Random random)
    {
        switch (random.Next(0, 12))
        {
            case 0:
                return 0d;
            case 1:
                return -0d;
            case 2:
                return 1d / 3d;
            case 3:
                return random.Next(-1000, 1000);
            default:
                var exponent = random.Next(-18, 19);
                var mantissa = (random.NextDouble() - 0.5) * 2;
                return Math.Round(mantissa * Math.Pow(10, exponent), random.Next(0, 6));
        }
    }

    /// <summary>
    /// A string long enough to need more than the smallest pooled buffer, with space runs in it so
    /// that it is written in several pieces rather than one.
    /// </summary>
    /// <returns>the string</returns>
    private static string LongText()
    {
        var text = new System.Text.StringBuilder(1400);
        for (var i = 0; i < 60; i++)
        {
            text.Append(CultureInfo.InvariantCulture, $"segment {i}  with  runs and a tab\there; ");
        }
        return text.ToString();
    }
}
