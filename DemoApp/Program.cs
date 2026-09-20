using OpenDocumentCreator;
using OpenDocumentCreator.DataTypes;
using OpenDocumentCreator.Styles;

// This DemoApp uses Telani.OpenDocumentCreator as a ProjectReference. That is
// not how a real consumer would integrate the package, but a ProjectReference
// is still the most convenient way to develop against the library. To verify
// the published .nupkg works, replace the ProjectReference in DemoApp.csproj
// with a PackageReference to Telani.OpenDocumentCreator.

Console.WriteLine("Telani.OpenDocumentCreator demo");
Console.WriteLine("================================");

var outDir = Path.Combine(AppContext.BaseDirectory, "out");
Directory.CreateDirectory(outDir);

var doc = new OpenDocumentSpreadsheet("Telani.OpenDocumentCreator DemoApp");

// Register a bold header style so we can show off styling.
var headerStyle = new OpenDocumentStyle
{
    Name = "Header",
    Family = StyleFamily.TableCell,
    TextProperties = new TextProperties
    {
        FontWeight = FontWeight.Bold,
    },
};
doc.Styles.Add(headerStyle.Name, headerStyle);

// Build a small grid: header row + a few rows of data.
// AutoGrid(doc, name, nRows, nColumns, defaultColumnWidth)
var grid = new AutoGrid(doc, "Planets", nRows: 9, nColumns: 3, "30mm");
doc.Tables.Add(grid);

grid.WriteCell(0, 0, new OpenDocumentCell("Name") { Style = headerStyle });
grid.WriteCell(1, 0, new OpenDocumentCell("Distance from Sun (AU)") { Style = headerStyle });
grid.WriteCell(2, 0, new OpenDocumentCell("Has Rings") { Style = headerStyle });

var planets = new (string Name, float DistanceAu, bool HasRings)[]
{
    ("Mercury", 0.39f, false),
    ("Venus",   0.72f, false),
    ("Earth",   1.00f, false),
    ("Mars",    1.52f, false),
    ("Jupiter", 5.20f, true),
    ("Saturn",  9.58f, true),
    ("Uranus", 19.22f, true),
    ("Neptune",30.05f, true),
};

for (int i = 0; i < planets.Length; i++)
{
    var (name, distance, rings) = planets[i];
    var y = i + 1;
    grid.WriteCell(0, y, new OpenDocumentCell(name));
    grid.WriteCell(1, y, new OpenDocumentCell { FloatContent = distance });
    grid.WriteCell(2, y, new OpenDocumentCell(rings ? "yes" : "no"));
}

var path = Path.Combine(outDir, "planets.ods");
await using (var fs = File.Create(path))
{
    await doc.Save(fs, leaveOpen: true);
}

Console.WriteLine($"Wrote {path}");
Console.WriteLine($"  Tables   : {doc.Tables.Count}");
Console.WriteLine($"  Columns  : {grid.Columns.Count}");
Console.WriteLine($"  Styles   : {doc.Styles.Count}");

// Round-trip sanity check: re-open the file as a ZIP and assert it contains
// the expected entries. A real OpenDocument file is a ZIP with content.xml,
// styles.xml, meta.xml, mimetype and META-INF/manifest.xml.
using (var zip = System.IO.Compression.ZipFile.OpenRead(path))
{
    var entries = zip.Entries.Select(e => e.FullName).OrderBy(s => s).ToArray();
    Console.WriteLine($"  Entries  : {string.Join(", ", entries)}");
}
