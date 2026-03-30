namespace Engine.Benchmark;

using BenchmarkDotNet.Attributes;
using Engine.Events;
using Engine.Grid;
using Engine.Parsers;
using Engine.Routing;
using Engine.Spawning;
using Engine.Vehicles;
using Core.Charging;

/// <summary>
/// Benchmark suite for OSRM router performance testing.
/// </summary>
[MemoryDiagnoser]
public class EVPopulatorBenchMark
{
    private const int _count = 10000;
    private EVPopulator _eVPopulator = null!;

    /// <summary>
    /// Initializes the benchmark setup with stations and EV coordinates.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var osrmPath = AppContext.GetData("OsrmDataPath") as string
            ?? throw new InvalidOperationException("OsrmDataPath not set in project.");
        var polygonPath = AppContext.GetData("GridPath") as string
            ?? throw new InvalidOperationException("GridPath not set in project.");
        var cityPath = AppContext.GetData("CityDataPath") as string
                    ?? throw new InvalidOperationException("GridPath not set in project.");

        var router = new OSRMRouter(new FileInfo(osrmPath), new List<Station>());
        var cities = CityParser.Parse(new FileInfo(cityPath));
        var polygons = PolygonParser.Parse(File.ReadAllText(polygonPath));
        var grid = Polygooner.GenerateGrid(0.1, polygons);
        var jp = new JourneyPipeline(grid, cities, router);

        var evFactory = new EVFactory(new Random(1), new JourneySamplerProvider(jp), router);
        var evStore = new EVStore(_count);
        var eventScheduler = new EventScheduler();

        _eVPopulator = new(evFactory, evStore, eventScheduler);
    }

    /// <summary>
    /// Benchmarks the creation of EVs using the EVPopulator.
    /// </summary>
    [Benchmark]
    public void CreateEVs() => _eVPopulator.CreateEVs(_count, distributionWindow: 1);
}
