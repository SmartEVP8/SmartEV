namespace Headless;

using BenchmarkDotNet.Running;
using Engine.Benchmark;

public static class Program
{
    public static async Task Main()
    {
        //BenchmarkRunner.Run<PolilineBufferBenchmark>();
        BenchmarkRunner.Run<StationsAroundPolyline>();
    }
}
