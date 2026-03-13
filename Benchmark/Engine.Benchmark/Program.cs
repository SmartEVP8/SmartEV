namespace Headless;

using BenchmarkDotNet.Running;
using Engine.Benchmark;

public static class Program
{
    public static async Task Main()
    {
        BenchmarkRunner.Run<StationsAroundPolyline>();
        BenchmarkRunner.Run<Polyline6Decode>();
        BenchmarkRunner.Run<Polyline6DecodeParallel>();
        BenchmarkRunner.Run<OsrmRouterBenchmark>();
    }
}
