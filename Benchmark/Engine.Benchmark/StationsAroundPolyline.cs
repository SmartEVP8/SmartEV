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
using Core.Vehicles.Configs;

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
    private Paths _path = null!;
    private EV _ev = null!;

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

        _router = new OSRMRouter(path);
        var route = _router.QuerySingleDestination(9.935932, 57.046707, 12.5683, 55.6761);
        var polyline = route.polyline;
        _path = Polyline6ToPoints.DecodePolyline(polyline);

        _ev = new EV(0, new Battery(100, 100, 15, Socket.CCS2), new Preferences(1f, 0.1f), new EVConfig("TestModel", 1f, "TestCategory", new BatteryConfig(50, 100, Socket.CCS2), 150));

        _stations = [];
        var rand = new Random(321);
        for (var i = 0; i < 4000; i++)
        {
            var lat = 55.95 + (rand.NextDouble() * 1);
            var lon = 8.36 + (rand.NextDouble() * 1.7);
            _stations.Add((ushort)i, new Station((ushort)i, $"Station{i}", $"Address{i}", new Position(lon, lat), null, 50f, rand));
        }

        var polygons = PolygonParser.Parse(File.ReadAllText(gridPath));
        var grid = Polygooner.GenerateGrid(0.1, polygons);
        _spatialGrid = new SpatialGrid(grid, _stations);
    }

    [GlobalCleanup]
    public void Cleanup() => _router?.Dispose();

    [Benchmark]
    public void BenchmarkStationsInPolyline()
    {
        var stationNearBy = _spatialGrid.GetStationsAlongPolyline(_path, 10);
        _ = ReachableStations.FindReachableStations(_path, _ev, _stations, stationNearBy, 10);
    }

}
