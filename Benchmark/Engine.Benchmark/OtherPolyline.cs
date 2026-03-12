using Engine.Parsers;
using Engine.Grid;
namespace Engine.Benchmark;

using BenchmarkDotNet.Attributes;
using Core.Charging;
using Core.Shared;
using Engine.Polyline;
using Core.Routing;
using Core.Utils;
using BenchmarkDotNet.Diagnosers;

/// <summary>
/// This benchmark class is designed to evaluate the performance of the PolylineBuffer.StationsInPolyline method,
/// which checks if any stations are within a certain radius of a polyline defined by a path's waypoints.
/// The benchmark includes a setup method to initialize the necessary data for the test, such as stations and EV coordinates.
/// The benchmark will measure the execution time of the StationsInPolyline method under controlled conditions,
/// allowing for performance analysis and optimization if needed.
/// </summary>
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
public class OtherPolylineBenchmark
{
    private Core.Routing.OSRMRouter _router = null!;
    private List<Station> _stations = null!;
    private SpatialGrid _spatialGrid = null!;
    private Paths _path = null!;
    /// <summary>
    /// Initializes the benchmark setup with stations and EV coordinates.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        // Initialize stations and EV coordinates here with 1000 stations and 1 route from 9.935932, 57.046707 to 10.2000, 56.1500
        var path = AppContext.GetData("OsrmDataPath") as string
            ?? throw new InvalidOperationException("OsrmDataPath not set in project.");
        _router = new OSRMRouter(path);
        var route = _router.QuerySingleDestination(9.935932, 57.046707, 12.5683, 55.6761);
        var polyline = route.polyline;
        _path = Polyline6ToPoints.DecodePolyline(polyline);

        _stations = new List<Station>();
        var rand = new Random();
        for (int i = 0; i < 4000; i++)
        {
            var lat = 55.0 + (rand.NextDouble() * (57.0 - 55.0));
            var lon = 9.0 + (rand.NextDouble() * (12.0 - 9.0));
            _stations.Add(new Station((ushort)i, $"Station{i}", $"Address{i}", new Position(lon, lat), null, 50f, rand));
        }
        var gridPath = AppContext.GetData("GridPath") as string
            ?? throw new InvalidOperationException("GridPath not set in project.");
        var polygons = PolygonParser.Parse(File.ReadAllText(gridPath));
        var grid = Polygooner.GenerateGrid(0.1, polygons);
        _spatialGrid = new SpatialGrid(grid, _stations);
    }

    [GlobalCleanup]
    public void Cleanup() => _router?.Dispose();

    [Benchmark]
    public void BenchmarkStationsInPolyline() => _ = NewPolyline.StationsInPolyline(_spatialGrid, _path, 10, 0.1, 0.1);
}
