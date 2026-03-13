
using BenchmarkDotNet.Running;
using Engine.Benchmark;
namespace Headless;

public static class Program
{
    public static async Task Main()
    {
        //BenchmarkRunner.Run<PolilineBufferBenchmark>();
        BenchmarkRunner.Run<OtherPolylineBenchmark>();
    }
}
