using BenchmarkDotNet.Attributes;
using Core.Routing;
namespace Core.Benchmark;

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
    private double[] _evCoordsFlat = null!;
    private double[] _stationCoordsFlat = null!;

    [GlobalSetup]
    public void Setup()
    {
        var path = AppContext.GetData("OsrmDataPath") as string
            ?? throw new InvalidOperationException("OsrmDataPath not set in project.");
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
        _evCoordsFlat = new double[1000 * 2];
        for (int i = 0; i < 1000; i++)
        {
            double lon = 9.9200 + (i * 0.002);
            double lat = 57.0400 + (i * 0.002);
            _evCoordinates[i] = (lon, lat);
            _evCoordsFlat[i * 2] = lon;
            _evCoordsFlat[i * 2 + 1] = lat;
        }

        _stationCoordsFlat = new double[50 * 2];
        for (int i = 0; i < 50; i++)
        {
            _stationCoordsFlat[i * 2] = stations[i].Position.Longitude;
            _stationCoordsFlat[i * 2 + 1] = stations[i].Position.Latitude;
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _router?.Dispose();
    }

    [Benchmark]
    public void Query1000Cars50StationsParallel()
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

    [Benchmark]
    public void Query1000Cars50StationsBulk()
    {
        _ = _router.QueryPointsToPoints(_evCoordsFlat, 1000, _stationCoordsFlat, 50);
    }

    [Benchmark]
    public void QuerySingleDestination()
    {
        var (lon, lat) = _evCoordinates[0];
        _ = _router.QuerySingleDestination(lon, lat, _stationCoordsFlat[0], _stationCoordsFlat[1]);
    }
}

public static class Program
{
    /// <summary>
    /// Runs the OSRM router benchmarks.
    /// </summary>
    public static void Main()
    {
        //       BenchmarkRunner.Run<OsrmRouterBenchmark>();
        BenchmarkRunner.Run<Polyline6DecodeTests>();
        BenchmarkRunner.Run<Polyline6DecodeParallelTests>();
    }
}
