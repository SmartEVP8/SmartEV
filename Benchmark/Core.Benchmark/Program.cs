namespace Simulation;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Core.Charging;
using Core.Shared;
using System.Collections.Generic;
using System.Linq;

[MemoryDiagnoser]
public class OsrmRouterBenchmark
{
    private OSRMRouter _router = null!;
    private int[] _stationIndices = null!;
    private (double Lon, double Lat)[] _evCoordinates = null!;

    [GlobalSetup]
    public void Setup()
    {
        var path = "/home/mertz/Coding/SmartEV/Core/data/output.osrm";
        _router = new OSRMRouter(path);

        var stations = new List<Station>(50);
        for (ushort i = 0; i < 50; i++)
        {
            stations.Add(new Station(
                id: i,
                name: string.Empty,
                address: string.Empty,
                position: new Position(9.9217 + (i * 0.001), 57.0488 + (i * 0.001)),
                chargers: []));
        }

        _router.InitStations(stations);
        _stationIndices = Enumerable.Range(0, 50).ToArray();

        _evCoordinates = new (double Lon, double Lat)[1000];
        for (int i = 0; i < 1000; i++)
        {
            _evCoordinates[i] = (9.9200 + (i * 0.002), 57.0400 + (i * 0.002));
        }
    }



    [GlobalCleanup]
    public void Cleanup()
    {
        _router?.Dispose();
    }

    [Benchmark]
    public void Query10000Cars50StationsParallel()
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };
        Parallel.For(0, _evCoordinates.Length, options, i =>
        {
            var (lon, lat) = _evCoordinates[i];
            _ = _router.QueryStations(lon, lat, _stationIndices);
        });
    }
}

public static class Program
{
    public static void Main()
    {
        BenchmarkRunner.Run<OsrmRouterBenchmark>();
    }
}
