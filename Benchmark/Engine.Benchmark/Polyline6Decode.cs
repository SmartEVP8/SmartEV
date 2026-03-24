namespace Engine.Benchmark;

using BenchmarkDotNet.Attributes;
using Engine.Utils;

/// <summary>
/// Benchmarks the performance of sequentially decoding a polyline string into a list of geographic points using the Polyline6 encoding algorithm.
/// </summary>
[MemoryDiagnoser]
public class Polyline6Decode
{
    private const string _polyline = "_p~iF~ps|U_ulLnnqC_mqNvxq`@";

    /// <summary>Gets or sets the total number of decodes to perform.</summary>
    private const int _totalDecodes = 10000;

    /// <summary>
    /// Benchmarks the performance of sequentially decoding a polyline string into a list of geographic points using the Polyline6 encoding algorithm.
    /// </summary>
    [Benchmark(Baseline = true)]
    public static void SequentialDecode()
    {
        for (var i = 0; i < _totalDecodes; i++)
        {
            Polyline6ToPoints.DecodePolyline(_polyline);
        }
    }
}