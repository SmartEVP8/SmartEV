namespace Engine.Benchmark;

using BenchmarkDotNet.Running;

/// <summary>
/// Engine.Benchmark is a collection of benchmarks related to the Engine.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs benchmarks.
    /// </summary>
    /// <returns>Benchmark results.</returns>
    public static async Task Main()
    {
        // BenchmarkRunner.Run<EVPopulatorBenchMark>();
        // BenchmarkRunner.Run<StationsAroundPolyline>();
        // BenchmarkRunner.Run<Polyline6Decode>();
        // BenchmarkRunner.Run<Polyline6DecodeParallel>();
        BenchmarkRunner.Run<OsrmRouterBenchmark>();
        // BenchmarkRunner.Run<OsrmRouterOneToManyBenchmark>();
        // BenchmarkRunner.Run<UpdateAllEVsBenchMark>();
        // BenchmarkRunner.Run<FindCandidateStationsBenchmark>();
    }
}
