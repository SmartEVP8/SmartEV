namespace Engine.Benchmark;

using BenchmarkDotNet.Attributes;
using Engine.Utils;

/// <summary>
/// Benchmarks parallel decoding of a polyline string using the Polyline6 encoding algorithm.
/// </summary>
[MemoryDiagnoser]
public class Polyline6DecodeParallel
{
    private const string _polyline = "_p~iF~ps|U_ulLnnqC_mqNvxq`@";
    private ParallelOptions _parallelOptions = new();

    /// <summary>Gets or sets the number of threads to use for parallel decoding.</summary>
    [Params(1, 2, 4, 8, 16)]
    public int Threads { get; set; }

    /// <summary>Gets or sets the total number of decodes to perform.</summary>
    [Params(10000, 100000)]
    public int TotalDecodes { get; set; }

    /// <summary>Sets up the parallel options before benchmarking.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = Threads };
    }

    /// <summary>Benchmarks parallel polyline decoding.</summary>
    [Benchmark]
    public void ParallelDecode()
    {
        Parallel.For(0, TotalDecodes, _parallelOptions, i =>
        {
            Polyline6ToPoints.DecodePolyline(_polyline);
        });
    }
}