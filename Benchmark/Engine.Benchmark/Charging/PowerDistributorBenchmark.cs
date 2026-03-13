namespace Engine.Benchmark.Charging;

using BenchmarkDotNet.Attributes;
using Core.Charging;

/// <summary>
/// Benchmark suite for PowerDistributor performance testing.
/// </summary>
[MemoryDiagnoser]
public class PowerDistributorBenchmark
{
    private const double _availablePower = 22.0;
    private const double _capacity1 = 11.0;
    private const double _capacity2 = 7.4;

    /// <summary>
    /// Benchmarks distributing power to a single consumer.
    /// </summary>
    [Benchmark]
    public void DistributeSingle() =>
        _ = PowerDistributor.DistributeSingle(_availablePower, _capacity1);

    /// <summary>
    /// Benchmarks distributing power between two consumers.
    /// </summary>
    [Benchmark]
    public void DistributeDual() =>
        _ = PowerDistributor.DistributeDual(_availablePower, _capacity1, _capacity2);
}