namespace Core.Benchmark;

using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Core.Charging;
using Core.Shared;

/// <summary>
/// Benchmark suite for OSRM router performance testing.
/// </summary>
[MemoryDiagnoser]
public class OsrmRouterBenchmark
{
    private Core.Routing.OSRMRouter _router = null!;
    private int[] _stationIndices = null!;
    private (double Lon, double Lat)[] _evCoordinates = null!;
    private double[] _evCoordsFlat = null!;
    private double[] _stationCoordsFlat = null!;

    /// <summary>
    /// Initializes the benchmark setup with stations and EV coordinates.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var path = AppContext.GetData("OsrmDataPath") as string
            ?? throw new InvalidOperationException("OsrmDataPath not set in project.");
        _router = new Routing.OSRMRouter(path);

        var stations = new List<Station>(50);
        for (ushort i = 0; i < 50; i++)
        {
            stations.Add(new Station(
                id: i,
                name: string.Empty,
                price: 0,
                random: new Random(i),
                address: string.Empty,
                position: new Position(9.9217 + (i * 0.001), 57.0488 + (i * 0.001)),
                chargers: [],
                price: 3.0f,
                random: new Random(i)));

        }

        _router.InitStations(stations);
        _stationIndices = Enumerable.Range(0, 50).ToArray();

        _evCoordinates = new (double Lon, double Lat)[1000];
        _evCoordsFlat = new double[1000 * 2];
        for (var i = 0; i < 1000; i++)
        {
            var lon = 9.9200 + (i * 0.002);
            var lat = 57.0400 + (i * 0.002);
            _evCoordinates[i] = (lon, lat);
            _evCoordsFlat[i * 2] = lon;
            _evCoordsFlat[(i * 2) + 1] = lat;
        }

        _stationCoordsFlat = new double[50 * 2];
        for (var i = 0; i < 50; i++)
        {
            _stationCoordsFlat[i * 2] = stations[i].Position.Longitude;
            _stationCoordsFlat[(i * 2) + 1] = stations[i].Position.Latitude;
        }
    }

    /// <summary>
    /// Cleans up resources after benchmarking.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => _router?.Dispose();

    /// <summary>
    /// Benchmarks querying stations for 1000 cars in parallel.
    /// </summary>
    [Benchmark]
    public void Query1000Cars50StationsParallel()
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
        };
        Parallel.For(0, _evCoordinates.Length, options, i =>
        {
            var (lon, lat) = _evCoordinates[i];
            _ = _router.QueryStations(lon, lat, _stationIndices);
        });
    }

    /// <summary>
    /// Benchmarks bulk querying of 1000 cars to 50 stations.
    /// </summary>
    [Benchmark]
    public void Query1000Cars50StationsBulk() => _ = _router.QueryPointsToPoints(_evCoordsFlat, _stationCoordsFlat);

    /// <summary>
    /// Benchmarks querying a single destination.
    /// </summary>
    [Benchmark]
    public void QuerySingleDestination()
    {
        var (lon, lat) = _evCoordinates[0];
        _ = _router.QuerySingleDestination(lon, lat, _stationCoordsFlat[0], _stationCoordsFlat[1]);
    }
}
