using BenchmarkDotNet.Running;

namespace OpenDocumentCreator.Benchmarks;

/// <summary>
/// Entry point for the benchmark suite.
///
/// Run everything:
///     dotnet run -c Release --project Benchmarks
///
/// Run one set (the filter matches the fully qualified method name):
///     dotnet run -c Release --project Benchmarks -- --filter *ColumnBenchmarks*
/// </summary>
internal static class Program
{
    private static void Main(string[] args)
        => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
