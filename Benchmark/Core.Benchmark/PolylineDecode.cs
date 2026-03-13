namespace Core.Benchmark;

using BenchmarkDotNet.Attributes;
using Core.Utils;
using System.Threading.Tasks;

[MemoryDiagnoser]
public class Polyline6DecodeTests
{
    private const string _polyline = "_p~iF~ps|U_ulLnnqC_mqNvxq`@";
    [Params(10000)]
    public int TotalDecodes;

    [Benchmark(Baseline = true)]
    public void SequentialDecode()
    {
        for (int i = 0; i < TotalDecodes; i++)
        {
            Polyline6ToPoints.DecodePolyline(_polyline);
        }
    }
}


[MemoryDiagnoser]
public class Polyline6DecodeParallelTests
{
    private const string _polyline = "_p~iF~ps|U_ulLnnqC_mqNvxq`@";

    [Params(1, 2, 4, 8, 16)]
    public int Threads;

    [Params(10000)]
    public int TotalDecodes;

    private ParallelOptions _parallelOptions;

    [GlobalSetup]
    public void Setup()
    {
        _parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Threads,
        };
    }

    [Benchmark]
    public void ParallelDecode()
    {
        Parallel.For(
            0,
            TotalDecodes,
            _parallelOptions,
            i =>
            {
                Polyline6ToPoints.DecodePolyline(_polyline);
            });
    }
}
