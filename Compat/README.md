# Comparing against a released version

Saves the same generated documents with a released build of this library and with the working
copy, and compares the results byte for byte. It exists to answer one question: did rewriting the
serializer change anything it was not meant to change?

## Why two projects

Both assemblies are called `OpenDocumentCreator`. MSBuild drops one of two references that share a
simple name, with or without `extern alias`, so the two versions cannot be compiled into the same
project. Each side therefore gets its own project, and the document-building code is a single file
(`Shared/Build.cs`) compiled into both. That is what makes the comparison meaningful: the two sides
are not written to look alike, they are the same source built against different libraries.

`Shared/Recipe.cs` describes a document without referring to either library, so one seed produces
the same document on both sides and any failure reproduces from the seed alone.

Neither project is in the solution: `Legacy` needs a released assembly on disk, which not every
checkout will have.

## Running it

```
dotnet run --project Compat/Legacy  -- <dir>/old 1 3000
dotnet run --project Compat/Current -- <dir>/new <dir>/old 1 3000
```

The second prints how many documents were identical and shows the first difference in each that
was not. It exits non-zero when anything differed.

By default the released side is `telani.opendocumentcreator/1.0.4` from the NuGet package folder.
Point it elsewhere with `-p:LegacyAssembly=<path to OpenDocumentCreator.dll>`.

`meta.xml` records the moment of saving, so those two timestamps are blanked before comparing.
Nothing else is normalized; every other byte must match.

## Putting the deliberate changes back first

Some changes since 1.0.4 alter the output **on purpose**, so a clean comparison needs them
temporarily reverted in the working copy. Without that, everything differs and the run says
nothing:

| change | revert for a comparison run |
|---|---|
| numbers held as `double` (#6) | `OpenDocumentCell.FloatContent` back to `float?`, `FormatCellValue` to take a `float`, and `AutoGrid.WriteCell`'s `double` case back to `Convert.ToSingle` |
| XML saved without indentation (#17) | `Indent = true` in `OpenDocument.WriteEntry` |
| generated style names (#14) | nothing to do - #30 put the 1.0.x naming back |

The reverts are a handful of lines plus a signature; they are deliberately not behind a compile-time
switch, because scaffolding for a one-off comparison does not belong in the shipped library.

## What the corpus covers

The corpus is widened rather than lengthened, because a longer run of the same documents finds
nothing. Counting distinct feature combinations against the seed they first appeared at showed the
earlier generator exhausting itself almost at once: every combination of cell kind, text and style
it could produce had been seen by seed 279, and every overwrite pairing by seed 693. Seeds 3,000 to
100,000 added no new one of either.

So the generator covers, instead:

- **text** that takes different routes through the writer: space runs at the start, in the middle
  and at the end, runs long enough for a two digit `text:c`, strings that are nothing but spaces or
  a single line break, leading and trailing blank lines, tabs, non breaking space, surrogate pairs,
  combining marks, right to left text, mixed line endings, and a string long enough to outgrow the
  first pooled buffer
- **numbers** across the exponent range, negative zero, and values that need many digits
- the **bulk writers**: `WriteColumn`, `WriteRow`, and `WriteRows` through both its array and its
  enumerable overload, which reach the grid differently
- `SetColumnsStyle`, and column widths starting at an offset rather than always at zero
- per cell `EmptyLineHandling` and `number-columns-repeated`
- several **tables** per document, one table in eight large enough to move the column padding
- **frames** carrying a text box or an image, not only a name
- **styles with properties on them**, across all six property groups, with parent styles and data
  style names; measurements in every unit and with several decimal places, colours including the
  transparent one, and border lines on every edge and the diagonal
- the families a spreadsheet uses rarely: `Table`, `Paragraph` and `Graphic`
- spans set **on the cell** as `ColumnsSpanned`, `RowsSpanned` and `IsCovered`, not only through
  `SetCellSpan`
- every way of filling a `Row`: `Add` in each overload, `InsertCell` with and without a style,
  `InsertCells`, `InsertCellsFromTemplateString`, and `Replace`
- **text documents**, one in twelve, which have a content writer of their own
- **table names the library rewrites**: the forbidden characters, the reserved `History`, the empty
  one, the 31 character limit, and names repeated so the unique name search has to run
- `table:shapes` on the table, and frames with geometry, a drawing id and style names, not only a
  name
- columns put on the table **by hand**, carrying the visibility, repeat count and default cell
  style that AutoGrid never sets
- the **document font**, which lands in `style:font-name`
- links that are `mailto:`, relative, or carry characters outside ASCII

The second half of that list came from reading a real caller of this library rather than from
guessing. It writes its spans on the cell and hardly ever calls `SetCellSpan`, it fills rows
through `InsertCell` far more than through `Add`, and above all it builds styles with real
properties - borders, measurements in mixed units, colours, parent styles - where the corpus had
been registering styles that carried nothing but a name and a family. None of the style
serialization was being compared at all.

Two things are deliberately left out. Values outside the range of a `float`, and the non finite
ones, are places the two versions are *meant* to differ, since the released one stores cell values
as a `float`. And `SetColumnsDefaultCellStyle` and the `WriteColumn` overload that carries a style
do not exist in 1.0.4 at all, so nothing can be compared against; they are covered by unit tests.

## What it found

With the deliberate changes reverted, **2993 of 3000** generated documents came out byte for byte
identical to 1.0.4, and the remaining 7 were refused by both versions for the same reason: a
document holding two tables whose names both normalize to `Tabelle 1`, which `GetUniqueTableName`
does not foresee because it compares the name before the setter rewrites it. Both versions agree,
which is what is being measured here.

The corpus matters more than the count. An early run of 2000 documents also reported everything
identical - and then reported everything identical again with the space encoder deliberately
broken, because nothing it generated ended in a *run* of spaces.

A comparison that cannot fail is not evidence. Break something on purpose before trusting a clean
run. Three that the corpus catches:

| deliberate break | documents differing, of 500 |
|---|---|
| `text:s` count written as `n` instead of `n-1` | 158 |
| `number-columns-repeated` written one too high | 372 |
| `TrimEnds` empty line handling treated as `Preserve` | 167 |
| border line written as style, width, colour instead of width, style, colour | 402 |
| measurements formatted `0.##` instead of round trip | 416 |
| spaces in a table name escaped to `-` instead of `_` | 149 |
| one letter changed in the text document's content | 40 |

Every one of those is reachable only because the corpus sets the property in question. Each of them
passed unnoticed against an earlier version of this corpus.

One caution about choosing a mutation. Routing `:` through `.` in `EscapeTableName` changed nothing
at all, because the routine maps `.` to `;` a few lines further down and both paths end on the same
character. The corpus was covering that name perfectly well; the mutation was the thing at fault. A
mutation that survives is worth a second look before it is read as a gap in the corpus.
