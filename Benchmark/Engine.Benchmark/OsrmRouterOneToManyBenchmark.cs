namespace Engine.Benchmark;

using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Core.Charging;
using Core.Shared;
using Engine.Routing;

/// <summary>
/// Benchmark suite for OSRM router one-to-many performance testing.
/// </summary>
[MemoryDiagnoser]
public class OsrmRouterOneToManyBenchmark
{
    private OSRMRouter _router = null!;
    private double[] _stationCoordsFlat = null!;
    private (double Lon, double Lat)[] _carCoordinates = null!;

    /// <summary>
    /// Initializes the benchmark setup with stations and a pool of car coordinates.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var osrmPath = AppContext.GetData("OsrmDataPath") as string
            ?? throw new InvalidDataException("OSRMPath not set.");

        var energyPrices = new EnergyPrices(
            new FileInfo(AppContext.GetData("EnergyPricesPath") as string
                ?? throw new InvalidDataException("EnergyPricesPath not set.")),
            new Random(1));

        var stations = new List<Station>(50);
        for (ushort i = 0; i < 50; i++)
        {
            stations.Add(new Station(
                id: i,
                name: string.Empty,
                address: string.Empty,
                position: new Position(9.9217 + (i * 0.001), 57.0488 + (i * 0.001)),
                chargers: [],
                energyPrices: energyPrices));
        }

        _router = new OSRMRouter(new FileInfo(osrmPath), stations);

        // Flat array of 50 station coordinates for one-to-many queries
        _stationCoordsFlat = new double[50 * 2];
        for (var i = 0; i < 50; i++)
        {
            _stationCoordsFlat[i * 2] = stations[i].Position.Longitude;
            _stationCoordsFlat[(i * 2) + 1] = stations[i].Position.Latitude;
        }

        // Pool of 10000 unique car positions to iterate over
        _carCoordinates = new (double Lon, double Lat)[10000];
        for (var i = 0; i < 10000; i++)
        {
            _carCoordinates[i] = (
                Lon: 9.9200 + (i * 0.0001),
                Lat: 57.0400 + (i * 0.0001)
            );
        }
    }

    /// <summary>
    /// Cleans up resources after benchmarking.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => _router?.Dispose();

    /// <summary>
    /// Benchmarks querying 1 car to 50 stations, repeated 10000 times.
    /// Each iteration uses a different car position from the pre-built pool.
    /// </summary>
    [Benchmark]
    public void Query1CarTo50Stations10000Times()
    {
        Parallel.For(0, 10000, (i) =>
        {
            var (lon, lat) = _carCoordinates[i];
            var carCoordsFlat = new double[] { lon, lat };
            _ = _router.QueryPointsToPoints(carCoordsFlat, _stationCoordsFlat);
        });
    }
}
