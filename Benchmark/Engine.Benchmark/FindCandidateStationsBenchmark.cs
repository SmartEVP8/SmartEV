namespace Engine.Benchmark;

using BenchmarkDotNet.Diagnosers;
using Engine.Events;
using Engine.Events.Middleware;
using Engine.Cost;
using Engine.Vehicles;
using BenchmarkDotNet.Attributes;
using Core.Routing;
using Core.Shared;
using Core.Vehicles;
using Core.Charging;
using Engine.Routing;
using Engine.Parsers;
using Engine.Grid;

/// <summary>
/// Benchmark for the FindCandidateStationsHandler,
/// which pre-computes candidate stations, computes their costs, and schedules a ReservationRequest.
/// Initializes OSRM router, station grid, and 5800 EVs to measure the full handler pipeline.
/// </summary>
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class FindCandidateStationsBenchmark
{
    private const int _count = 5800;
    private EventScheduler _eventScheduler = null!;
    private EVStore _evStore = null!;
    private FindCandidateStationsHandler _findCandidateStationsHandler = null!;

    /// <summary>
    /// Dependency setup for the benchmark.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var path = AppContext.GetData("OsrmDataPath") as string
            ?? throw new InvalidOperationException("OsrmDataPath not set in project.");
        var gridPath = AppContext.GetData("GridPath") as string
            ?? throw new InvalidOperationException("GridPath not set in project.");
        var csvPath = AppContext.GetData("EnergyPricesPath") as string
            ?? throw new InvalidOperationException("EnergyPricesPath not set in project.");
        var energyPrices = new EnergyPrices(new FileInfo(csvPath), new Random(42));

        var router = new OSRMRouter(new FileInfo(path), []);
        var stations = new Dictionary<ushort, Station>();
        var rand = new Random(321);
        for (var i = 0; i < 4000; i++)
        {
            var lat = 55.95 + (rand.NextDouble() * 1);
            var lon = 8.36 + (rand.NextDouble() * 1.7);
            stations.Add((ushort)i, new Station((ushort)i, string.Empty, string.Empty, new Position(lon, lat), [], energyPrices));
        }

        var polygons = PolygonParser.Parse(File.ReadAllText(gridPath));
        var grid = Polygooner.GenerateGrid(0.1, polygons);
        var spatialGrid = new SpatialGrid(grid, stations);

        var costWeigths = new CostWeights(PathDeviation: 1);
        var costStore = new CostStore(costWeigths);
        var computeCost = new ComputeCost(costStore);

        _eventScheduler = new EventScheduler();
        _evStore = new EVStore(_count);

        var findCandidateStationService = new FindCandidateStationService(router, stations, spatialGrid, _evStore);
        _findCandidateStationsHandler = new FindCandidateStationsHandler(findCandidateStationService, computeCost, _eventScheduler, _evStore);

        var random = new Random(1);
        for (var i = 0; i < _count; i++)
        {
            var battery = new Battery(100, 50, 10 * random.NextSingle(), Socket.CCS2);
            var preferences = new Preferences(0.5f, 0.1f, 10.0f);
            var journey = new Journey(new Time(0), new Time(100), new Paths([new Position(10 * random.NextSingle(), 10 * random.NextSingle()), new Position(20 * random.NextSingle(), 20 * random.NextSingle())]));
            var ev = new EV(battery, preferences, journey, 150);
            _evStore.Set(i, ref ev);
        }
    }

    /// <summary>
    /// Ensure that we have a clean EventScheduler for each iteration of the benchmark.
    /// </summary>
    [IterationCleanup]
    public void IterationCleanup() => _eventScheduler = new EventScheduler();

    /// <summary>
    /// Benchmarks the FindCandidateStationsHandler by invoking its Handle method with FindCandidateStations events.
    /// Measures the full pipeline: pre-computation via OSRM router, cache retrieval, cost computation, and reservation scheduling.
    /// </summary>
    [Benchmark]
    public void FindCandidateStationsEventScheduling()
    {
        for (var i = 0; i < _count; i++)
        {
            var ev = new FindCandidateStations(i, new Time(10));
            _findCandidateStationsHandler.Handle(ev);
        }
    }
}
