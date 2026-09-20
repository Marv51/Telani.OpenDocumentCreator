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
    WriteRow,
    WriteRowsArray,
    WriteRowsEnumerable,
}

internal sealed record CellStep(
    int X,
    int Y,
    CellKind Kind,
    string Text,
    double Number,
    int StyleIndex,
    EmptyLineMode EmptyLines,
    int Repeat,
    FrameKind Frame);

internal sealed record SpanStep(int X, int Y, int RowSpan, int ColumnSpan);

/// <summary>
/// A bulk write. <see cref="Values"/> is a list of rows of strings; the single row writers use the
/// first one and WriteColumn uses the first column of it.
/// </summary>
internal sealed record BulkStep(BulkKind Kind, int X, int Y, IReadOnlyList<IReadOnlyList<string>> Values, int StyleIndex);

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
    int CellStyleCount,
    int ColumnStyleCount,
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

        var cellStyleCount = random.Next(0, 5);
        var columnStyleCount = random.Next(0, 4);

        var tables = new List<TableRecipe>();
        var tableCount = random.Next(1, 4);
        for (var t = 0; t < tableCount; t++)
        {
            tables.Add(GenerateTable(random, t, cellStyleCount, columnStyleCount));
        }

        return new Recipe(seed, cellStyleCount, columnStyleCount, tables);
    }

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
                (FrameKind)random.Next(Enum.GetValues<FrameKind>().Length)));
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

        return new BulkStep(kind, x, y, values, cellStyleCount == 0 ? -1 : random.Next(-1, cellStyleCount));
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
