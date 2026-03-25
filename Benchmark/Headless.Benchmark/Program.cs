namespace Headless.Benchmark;

using BenchmarkDotNet.Running;

/// <summary>
/// Benchmark class.
/// </summary>
public class Program
{
    /// <summary>
    /// The main entry point for the benchmark application.
    /// </summary>
    public static void Main() => BenchmarkRunner.Run<OSRMRouterBenchmark>();
}
