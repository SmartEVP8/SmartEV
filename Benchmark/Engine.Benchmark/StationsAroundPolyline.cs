namespace Engine.Benchmark;

using Engine.Parsers;
using Engine.Grid;
using BenchmarkDotNet.Attributes;
using Core.Charging;
using Core.Shared;
using BenchmarkDotNet.Diagnosers;
using Engine.Routing;
using Engine.Utils;
using Core.Vehicles;
using Core.Routing;

/// <summary>
/// This benchmark class is designed to evaluate the performance of the PolylineBuffer.StationsInPolyline method,
/// which checks if any stations are within a certain radius of a polyline defined by a path's waypoints.
/// The benchmark includes a setup method to initialize the necessary data for the test, such as stations and EV coordinates.
/// The benchmark will measure the execution time of the StationsInPolyline method under controlled conditions,
/// allowing for performance analysis and optimization if needed.
/// </summary>
[MemoryDiagnoser]
public class StationsAroundPolyline
{
    private OSRMRouter _router = null!;
    private Dictionary<ushort, Station> _stations = null!;
    private SpatialGrid _spatialGrid = null!;
    private List<Position> _waypoints = null!;
    private EV _ev = default;

    /// <summary>
    /// Initializes the benchmark setup with stations and EV coordinates.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        // Initialize stations and EV coordinates here with 1000 stations and 1 route from 9.935932, 57.046707 to 10.2000, 56.1500
        var path = AppContext.GetData("OsrmDataPath") as string
            ?? throw new InvalidOperationException("OsrmDataPath not set in project.");
        var gridPath = AppContext.GetData("GridPath") as string
            ?? throw new InvalidOperationException("GridPath not set in project.");
        var csvPath = AppContext.GetData("CSVPath") as string
            ?? throw new InvalidOperationException("CSVPath not set in project.");
        var energyPrices = new EnergyPrices(new FileInfo(csvPath), new Random(42));
        _router = new OSRMRouter(new FileInfo(path), []);
        var route = _router.QuerySingleDestination(9.935932, 57.046707, 12.5683, 55.6761);
        var polyline = route.Polyline;
        _waypoints = Polyline6ToPoints.DecodePolyline(polyline);
        var journey = new Journey(0, 0, 0, _waypoints);

        _ev = new EV(new Battery(100, 100, 15), new Preferences(1f, 0.1f, 10.0f), journey, 150);

        _stations = [];
        var rand = new Random(321);
        for (var i = 0; i < 4000; i++)
        {
            var lat = 55.95 + (rand.NextDouble() * 1);
            var lon = 8.36 + (rand.NextDouble() * 1.7);
            _stations.Add((ushort)i, new Station((ushort)i, string.Empty, string.Empty, new Position(lon, lat), [], energyPrices));
        }

        var polygons = PolygonParser.Parse(File.ReadAllText(gridPath));
        var grid = Polygooner.GenerateGrid(0.1, polygons);
        _spatialGrid = new SpatialGrid(grid, _stations);
    }

    /// <summary>
    /// Cleans up resources after the benchmark is complete.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup() => _router?.Dispose();

    /// <summary>
    /// Benchmarks the StationsInPolyline method to evaluate its performance in finding stations within a certain radius of a polyline defined by a path's waypoints.
    /// </summary>
    [Benchmark]
    public void BenchmarkStationsInPolyline()
    {
        var stationNearBy = _spatialGrid.GetStationsAlongPolyline(_waypoints, _ev.Preferences.MaxPathDeviation);
        _ = ReachableStations.FindReachableStations(_waypoints, _ev, _stations, stationNearBy, _ev.Preferences.MaxPathDeviation);
    }
}
