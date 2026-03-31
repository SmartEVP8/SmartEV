namespace Engine.Benchmark;

using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Core.Charging;
using Core.Shared;
using Engine.Routing;

/// <summary>
/// Benchmark suite for OSRM router performance testing.
/// </summary>
[MemoryDiagnoser]
public class OsrmRouterBenchmark
{
    private static readonly double[] _destPosition = [10.1572, 56.1496];

    private OSRMRouter _router = null!;
    private ushort[] _stationIndices = null!;
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

        var energyPrices = new EnergyPrices(
            new FileInfo(AppContext.GetData("EnergyPricesPath") as string
                ?? throw new InvalidDataException("EnergyPricesPath not set.")),
            new Random(1));

        var stations = new List<Station>(200);
        for (ushort i = 0; i < 200; i++)
        {
            stations.Add(new Station(
                id: i,
                name: string.Empty,
                address: string.Empty,
                position: new Position(9.9217 + (i * 0.001), 57.0488 + (i * 0.001)),
                chargers: [],
                energyPrices: energyPrices));
        }

        _router = new OSRMRouter(new FileInfo(path), stations);
        _stationIndices = [.. Enumerable.Range(0, 200).Select(i => (ushort)i)];

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

        _stationCoordsFlat = new double[200 * 2];
        for (var i = 0; i < 200; i++)
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
    /// Benchmarks bulk querying of 1000 cars to N stations.
    /// </summary>
    [Benchmark]
    public void Query1000CarsToNStationsBulk()
    {
        _ = _router.QueryPointsToPoints(_evCoordsFlat, _stationCoordsFlat);
    }

    /// <summary>
    /// Benchmarks querying a single destination.
    /// </summary>
    [Benchmark]
    public void QuerySingleDestination()
    {
        var (lon, lat) = _evCoordinates[0];
        _ = _router.QuerySingleDestination(lon, lat, _stationCoordsFlat[0], _stationCoordsFlat[1]);
    }

    /// <summary>
    /// Benchmarks querying to a specific station and destination.
    /// </summary>
    [Benchmark]
    public void QueryStationsWithDest()
    {
        var (lon, lat) = _evCoordinates[0];
        _ = _router.QueryStationsWithDest(lon, lat, _destPosition[0], _destPosition[1], _stationIndices);
    }

    /// <summary>
    /// Benchmarks multi-stop waypoint routing EV -> Station -> Dest.
    /// </summary>
    [Benchmark]
    public void QueryDestinationWithWaypoint()
    {
        var (lon, lat) = _evCoordinates[0];
        _ = _router.QueryDestination([lon, lat, _stationCoordsFlat[0], _stationCoordsFlat[1], _destPosition[0], _destPosition[1]]);
    }
}