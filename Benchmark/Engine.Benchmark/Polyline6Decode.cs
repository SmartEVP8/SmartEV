namespace Engine.Benchmark;

using BenchmarkDotNet.Attributes;
using Engine.Utils;

[MemoryDiagnoser]
public class Polyline6Decode
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
