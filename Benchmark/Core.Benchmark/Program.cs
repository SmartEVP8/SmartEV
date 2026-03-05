namespace Core.Benchmark;

using BenchmarkDotNet.Running;

/// <summary>
/// Entry point for the benchmark application.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the OSRM router benchmarks.
    /// </summary>
    public static void Main() => BenchmarkRunner.Run<OsrmRouterBenchmark>();
}