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

internal sealed record CellStep(int X, int Y, CellKind Kind, string Text, double Number, int StyleIndex);

internal sealed record SpanStep(int X, int Y, int RowSpan, int ColumnSpan);

/// <summary>
/// A whole document, described without using either library.
/// </summary>
internal sealed record Recipe(
    int Seed,
    string TableName,
    int Rows,
    int Columns,
    string DefaultWidth,
    int CellStyleCount,
    IReadOnlyList<string> ColumnWidths,
    IReadOnlyList<CellStep> Cells,
    IReadOnlyList<SpanStep> Spans,
    IReadOnlyList<int> StyledRows)
{
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
        "tab	and	tab",
        "x",
        string.Empty,
    ];

    private static readonly string[] Widths = ["11mm", "20mm", "35mm", "7mm", "120mm"];

    /// <summary>
    /// Builds a random recipe. The same seed always gives the same document, so a failure can be
    /// reproduced from the seed alone.
    /// </summary>
    /// <param name="seed">the seed to generate from</param>
    /// <returns>the recipe</returns>
    public static Recipe Generate(int seed)
    {
        var random = new Random(seed);

        var rows = random.Next(1, 12);
        var columns = random.Next(1, 10);
        var styleCount = random.Next(0, 4);

        var widths = new List<string>();
        for (var i = 0; i < random.Next(0, Math.Min(columns, 4)); i++)
        {
            widths.Add(Widths[random.Next(Widths.Length)]);
        }

        var cells = new List<CellStep>();
        for (var i = 0; i < random.Next(0, rows * columns + 1); i++)
        {
            var kind = (CellKind)random.Next(Enum.GetValues<CellKind>().Length);
            cells.Add(new CellStep(
                random.Next(0, columns),
                random.Next(0, rows),
                kind,
                Texts[random.Next(Texts.Length)],
                Math.Round((random.NextDouble() - 0.5) * 10000, random.Next(0, 4)),
                styleCount == 0 ? -1 : random.Next(-1, styleCount)));
        }

        var spans = new List<SpanStep>();
        for (var i = 0; i < random.Next(0, 3); i++)
        {
            var x = random.Next(0, columns);
            var y = random.Next(0, rows);
            spans.Add(new SpanStep(x, y, random.Next(1, 3), random.Next(1, Math.Max(2, columns - x))));
        }

        var styledRows = new List<int>();
        for (var i = 0; i < random.Next(0, 3); i++)
        {
            styledRows.Add(random.Next(0, rows));
        }

        return new Recipe(
            seed,
            "Sheet" + random.Next(0, 100),
            rows,
            columns,
            Widths[random.Next(Widths.Length)],
            styleCount,
            widths,
            cells,
            spans,
            styledRows);
    }
}
