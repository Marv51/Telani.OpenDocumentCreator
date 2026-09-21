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
    CellProps? Cell,
    ParagraphProps? Paragraph,
    TextProps? Text,
    ColumnProps? Column,
    RowProps? Row,
    GraphicProps? Graphic);

internal sealed record CellProps(
    LineRecipe? Border,
    LineRecipe? BorderLeft,
    LineRecipe? BorderTop,
    LineRecipe? Diagonal,
    ColorRecipe? BackgroundColor,
    int? VerticalAlign,
    int? WrapOption,
    int? TextAlignSource,
    int? CellProtect,
    int? RotationAlign,
    string? RotationAngle,
    string? Padding,
    string? PaddingLeft,
    string? DecimalPlaces,
    int? ShrinkToFit,
    int? PrintContent);

internal sealed record ParagraphProps(int? TextAlign, MeasureRecipe? MarginLeft, string? LineBreak);

internal sealed record TextProps(
    int? FontWeight,
    int? FontStyle,
    MeasureRecipe? FontSize,
    string? FontFamily,
    ColorRecipe? Color,
    ColorRecipe? BackgroundColor,
    string? Language,
    int? UnderlineStyle,
    string? LetterSpacing);

internal sealed record ColumnProps(MeasureRecipe? ColumnWidth, int? UseOptimal, int? BreakBefore, string? RelativeColumnWidth);

internal sealed record RowProps(MeasureRecipe? RowHeight, MeasureRecipe? MinRowHeight, int? UseOptimal, ColorRecipe? BackgroundColor, int? BreakBefore);

internal sealed record GraphicProps(int? Fill, ColorRecipe? FillColor, int? Stroke, MeasureRecipe? StrokeWidth, ColorRecipe? StrokeColor, string? Opacity);

internal sealed record CellStep(
    int X,
    int Y,
    CellKind Kind,
    string Text,
    double Number,
    int StyleIndex,
    EmptyLineMode EmptyLines,
    int Repeat,
    FrameKind Frame,
    int ColumnsSpanned,
    int RowsSpanned,
    bool IsCovered);

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
internal sealed record Recipe(
    int Seed,
    IReadOnlyList<StyleRecipe> CellStyles,
    IReadOnlyList<StyleRecipe> ColumnStyles,
    IReadOnlyList<StyleRecipe> RowStyles,
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

        // The families a spreadsheet uses more rarely. No cell refers to them; they are here
        // because a style still has to serialize, and each family writes a different properties
        // element.
        var extra = new List<StyleRecipe>();
        if (random.Next(0, 2) == 0)
        {
            extra.AddRange(Styles(random, StyleTarget.Table, "ta", 1));
        }
        if (random.Next(0, 2) == 0)
        {
            extra.AddRange(Styles(random, StyleTarget.Paragraph, "P", 1));
        }
        if (random.Next(0, 2) == 0)
        {
            extra.AddRange(Styles(random, StyleTarget.Graphic, "gr", 1));
        }

        var tables = new List<TableRecipe>();
        var tableCount = random.Next(1, 4);
        for (var i = 0; i < tableCount; i++)
        {
            tables.Add(GenerateTable(random, i, cellStyles.Count, columnStyles.Count));
        }

        return new Recipe(seed, cellStyles, columnStyles, rowStyles, extra, tables);
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
                family == StyleTarget.TableCell ? CellProperties(random) : null,
                family is StyleTarget.TableCell or StyleTarget.Paragraph ? ParagraphProperties(random) : null,
                family is StyleTarget.TableCell or StyleTarget.Paragraph ? TextProperties(random) : null,
                family == StyleTarget.TableColumn ? ColumnProperties(random) : null,
                family == StyleTarget.TableRow ? RowProperties(random) : null,
                family == StyleTarget.Graphic ? GraphicProperties(random) : null));
        }

        return styles;
    }

    private static CellProps CellProperties(Random random) => new(
        MaybeLine(random),
        MaybeLine(random),
        MaybeLine(random),
        MaybeLine(random),
        MaybeColor(random),
        MaybeEnum(random),
        MaybeEnum(random),
        MaybeEnum(random),
        MaybeEnum(random),
        MaybeEnum(random),
        MaybeOne(random, Angles),
        MaybeOne(random, Paddings),
        MaybeOne(random, Paddings),
        MaybeOne(random, Counts),
        MaybeEnum(random),
        MaybeEnum(random));

    private static ParagraphProps ParagraphProperties(Random random) => new(
        MaybeEnum(random),
        MaybeMeasure(random),
        MaybeOne(random, Counts));

    private static TextProps TextProperties(Random random) => new(
        MaybeEnum(random),
        MaybeEnum(random),
        MaybeMeasure(random),
        MaybeOne(random, Fonts),
        MaybeColor(random),
        MaybeColor(random),
        MaybeOne(random, Languages),
        MaybeEnum(random),
        MaybeOne(random, Paddings));

    private static ColumnProps ColumnProperties(Random random) => new(
        MaybeMeasure(random),
        MaybeEnum(random),
        MaybeEnum(random),
        MaybeOne(random, RelativeWidths));

    private static RowProps RowProperties(Random random) => new(
        MaybeMeasure(random),
        MaybeMeasure(random),
        MaybeEnum(random),
        MaybeColor(random),
        MaybeEnum(random));

    private static GraphicProps GraphicProperties(Random random) => new(
        MaybeEnum(random),
        MaybeColor(random),
        MaybeEnum(random),
        MaybeMeasure(random),
        MaybeColor(random),
        MaybeOne(random, Opacities));

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

    private static TableRecipe GenerateTable(Random random, int index, int cellStyleCount, int columnStyleCount)
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

        var cellCap = large ? 800 : 300;
        var cells = new List<CellStep>();
        for (var i = 0; i < random.Next(0, Math.Min(rows * columns, cellCap) + 1); i++)
        {
            var kind = (CellKind)random.Next(Enum.GetValues<CellKind>().Length);
            cells.Add(new CellStep(
                random.Next(0, columns),
                random.Next(0, rows),
                kind,
                Texts[random.Next(Texts.Length)],
                Number(random),
                cellStyleCount == 0 ? -1 : random.Next(-1, cellStyleCount),
                (EmptyLineMode)random.Next(Enum.GetValues<EmptyLineMode>().Length),
                // A repeat of 1 is the ordinary case; the rest exercise number-columns-repeated.
                random.Next(0, 6) == 0 ? random.Next(1, 5) : 1,
                (FrameKind)random.Next(Enum.GetValues<FrameKind>().Length),
                // The real callers mostly span by setting these on the cell rather than by
                // calling SetCellSpan, so both routes are worth having.
                random.Next(0, 8) == 0 ? random.Next(1, 4) : 1,
                random.Next(0, 10) == 0 ? random.Next(1, 3) : 1,
                random.Next(0, 12) == 0));
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
            "Sheet" + index + "_" + random.Next(0, 100),
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
