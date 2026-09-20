using System.Globalization;

namespace OdcCompat;

/// <summary>
/// Writes a range of generated documents using the released library.
/// </summary>
internal static class Program
{
    private static void Main(string[] args)
        => Build.WriteAll(args[0], int.Parse(args[1], CultureInfo.InvariantCulture), int.Parse(args[2], CultureInfo.InvariantCulture));
}
