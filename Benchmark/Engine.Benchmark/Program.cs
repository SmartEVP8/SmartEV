
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Running;
using Engine.Benchmark;
namespace Headless;

using Core.Charging;
using Core.Routing;
using Core.Shared;
using Engine.Grid;
using Engine.Parsers;

public static class Program
{
    public static async Task Main()
    {
        BenchmarkRunner.Run<PolilineBufferBenchmark>();
    }
}
