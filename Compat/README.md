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

Three changes since 1.0.4 alter the output **on purpose**, so a clean comparison needs them
temporarily reverted in the working copy. Without that, everything differs and the run says
nothing:

| change | revert for a comparison run |
|---|---|
| numbers held as `double` (#6) | `OpenDocumentCell.FloatContent` back to `float?`, `FormatCellValue` to take a `float`, and `AutoGrid.WriteCell`'s `double` case back to `Convert.ToSingle` |
| XML saved without indentation (#17) | `Indent = true` in `OpenDocument.WriteEntry` |
| generated style names (#14) | already restored - the style count is the first candidate again |

The reverts are three lines plus a signature; they are deliberately not behind a compile-time
switch, because scaffolding for a one-off comparison does not belong in the shipped library.

## What it found

With those three reverted, **3000 of 3000** generated documents came out byte for byte identical
to 1.0.4.

The corpus matters more than the count. An early run of 2000 documents also reported everything
identical - and then reported everything identical again with the space encoder deliberately
broken, because nothing it generated ended in a *run* of spaces. The text corpus now includes runs
at the start, in the middle and at the end, strings that are nothing but spaces, and tabs. With
that, the same deliberate break shows up in 331 of 800 documents.

A comparison that cannot fail is not evidence. Break something on purpose before trusting a clean
run.
