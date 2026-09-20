using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace OdcCompat;

/// <summary>
/// Writes a range of generated documents with the working copy, then compares them against the
/// same range written by the released library.
///
/// Run the released side first:
///     dotnet run --project Compat/Legacy -- &lt;dir&gt;/old 1 500
///     dotnet run --project Compat/Current -- &lt;dir&gt;/new &lt;dir&gt;/old 1 500
///
/// The working copy is expected to be on a branch where the deliberate behaviour changes have
/// been put back - numbers as float, the saved XML indented, the generated style names counting
/// styles - so that anything that still differs is something the rewrite broke rather than
/// something it was meant to change.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        var newDirectory = args[0];
        var oldDirectory = args[1];
        var firstSeed = int.Parse(args[2], CultureInfo.InvariantCulture);
        var count = int.Parse(args[3], CultureInfo.InvariantCulture);

        Build.WriteAll(newDirectory, firstSeed, count);

        var identical = 0;
        var differing = new List<string>();
        var bothThrew = 0;
        var oneThrew = new List<string>();

        for (var seed = firstSeed; seed < firstSeed + count; seed++)
        {
            var name = seed.ToString(CultureInfo.InvariantCulture) + ".ods";
            var oldPath = Path.Combine(oldDirectory, name);
            var newPath = Path.Combine(newDirectory, name);

            var oldThrew = File.Exists(oldPath + ".threw");
            var newThrew = File.Exists(newPath + ".threw");

            if (oldThrew || newThrew)
            {
                if (oldThrew && newThrew)
                {
                    bothThrew++;
                }
                else
                {
                    oneThrew.Add($"seed {seed}: released {(oldThrew ? "threw" : "succeeded")}, working copy {(newThrew ? "threw" : "succeeded")}");
                }
                continue;
            }

            var difference = FirstDifferingPart(File.ReadAllBytes(oldPath), File.ReadAllBytes(newPath));
            if (difference is null)
            {
                identical++;
            }
            else
            {
                differing.Add($"seed {seed}: {difference}");
            }
        }

        Console.WriteLine($"identical:  {identical}");
        Console.WriteLine($"differing:  {differing.Count}");
        Console.WriteLine($"both threw: {bothThrew}");
        Console.WriteLine($"one threw:  {oneThrew.Count}");

        foreach (var line in oneThrew.Take(10))
        {
            Console.WriteLine("  " + line);
        }
        foreach (var line in differing.Take(10))
        {
            Console.WriteLine("  " + line);
        }

        return differing.Count == 0 && oneThrew.Count == 0 ? 0 : 1;
    }

    private static string? FirstDifferingPart(byte[] oldBytes, byte[] newBytes)
    {
        using var oldZip = new ZipArchive(new MemoryStream(oldBytes), ZipArchiveMode.Read);
        using var newZip = new ZipArchive(new MemoryStream(newBytes), ZipArchiveMode.Read);

        var oldNames = oldZip.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var newNames = newZip.Entries.Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal).ToList();

        if (!oldNames.SequenceEqual(newNames, StringComparer.Ordinal))
        {
            return "different parts: [" + string.Join(", ", oldNames) + "] vs [" + string.Join(", ", newNames) + "]";
        }

        foreach (var name in oldNames)
        {
            var a = ReadEntry(oldZip, name);
            var b = ReadEntry(newZip, name);

            if (name == "meta.xml")
            {
                // meta.xml stamps the moment of saving, so the two runs differ there by
                // construction. Only those two elements are blanked; the rest is compared.
                var oldMeta = WithoutTimestamps(Encoding.UTF8.GetString(a));
                var newMeta = WithoutTimestamps(Encoding.UTF8.GetString(b));
                if (!string.Equals(oldMeta, newMeta, StringComparison.Ordinal))
                {
                    return name + " " + DescribeFirstDifference(Encoding.UTF8.GetBytes(oldMeta), Encoding.UTF8.GetBytes(newMeta));
                }
                continue;
            }

            if (!a.AsSpan().SequenceEqual(b))
            {
                return name + " " + DescribeFirstDifference(a, b);
            }
        }

        return null;
    }

    private static string WithoutTimestamps(string metaXml)
        => System.Text.RegularExpressions.Regex.Replace(
            metaXml,
            "(<(?:meta:creation-date|dc:date)>)[^<]*(</)",
            "$1WHEN$2");

    private static byte[] ReadEntry(ZipArchive zip, string name)
    {
        using var stream = zip.GetEntry(name)!.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static string DescribeFirstDifference(byte[] a, byte[] b)
    {
        var oldText = Encoding.UTF8.GetString(a);
        var newText = Encoding.UTF8.GetString(b);

        var i = 0;
        while (i < oldText.Length && i < newText.Length && oldText[i] == newText[i])
        {
            i++;
        }

        var from = Math.Max(0, i - 50);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"differs at {i} (lengths {oldText.Length}/{newText.Length}){Environment.NewLine}      old: {Excerpt(oldText, from)}{Environment.NewLine}      new: {Excerpt(newText, from)}");
    }

    private static string Excerpt(string text, int from)
    {
        var slice = text.AsSpan(Math.Min(from, text.Length));
        return slice[..Math.Min(130, slice.Length)]
            .ToString()
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
