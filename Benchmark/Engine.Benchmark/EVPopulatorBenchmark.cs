namespace Engine.Benchmark;

using BenchmarkDotNet.Attributes;
using Engine.Grid;
using Engine.Parsers;
using Engine.Routing;
using Engine.Vehicles;

/// <summary>
/// Benchmark suite for OSRM router performance testing.
/// </summary>
[MemoryDiagnoser]
public class EVPopulatorBenchMark
{
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


        var router = new OSRMRouter(osrmPath);
        var cities = CityParser.Parse(new FileInfo(cityPath));
        var polygons = PolygonParser.Parse(File.ReadAllText(polygonPath));
        var grid = Polygooner.GenerateGrid(0.1, polygons);
        var jp = new JourneyPipeline(grid, cities, router);
        var samplers = jp.Compute(1.0f) ?? throw new InvalidOperationException("Journey samplers could not be computed.");
        _eVPopulator = new(new Random(1), 10000, samplers, router);
    }

    [Benchmark]
    public void CreateEVs() => _eVPopulator.CreateEVs(10000);
}
