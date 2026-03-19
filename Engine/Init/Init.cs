namespace Engine.Init;

using Core.Charging;
using Core.Shared;
using Engine.Cost;
using Engine.Events;
using Engine.Grid;
using Engine.Metrics;
using Engine.Parsers;
using Engine.Routing;
using Engine.Spawning;
using Engine.StationFactory;
using Engine.Vehicles;
using Microsoft.Extensions.DependencyInjection;

public static class Init
{
    public static void InitEngine(IServiceCollection services)
    {
        services.AddSingleton<EventScheduler>();

        services.AddSingleton<IOSRMRouter>(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            return new OSRMRouter(settings.OsrmPath);
        });

        services.AddSingleton<ICostStore>(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            return new CostStore(settings.CostConfig);
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            var metricsConfig = settings.MetricsConfig;
            var runId = settings.RunId;
            return new MetricsService(metricsConfig, runId);
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            var random = settings.Seed;
            return new EVFactory(random);
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            var path = settings.EnergyPricesPath;
            return new EnergyPrices(path);
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            var energyPrices = sp.GetRequiredService<EnergyPrices>();
            var stationFactory = new StationFactory(settings.StationFactoryOptions, settings.Seed, energyPrices);
            var stations = stationFactory.CreateStations(settings.StationsPath);
            return new SpatialGrid(InitSpawnGrid(settings.PolygonPath), stations);
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            var router = sp.GetRequiredService<IOSRMRouter>();
            var spawnGrid = InitSpawnGrid(settings.PolygonPath);
            var cities = InitCities(settings.CitiesPath);
            return new JourneyPipeline(spawnGrid, cities, router);
        });
    }

    private static SpawnGrid InitSpawnGrid(FileInfo polygonPath)
    {
        var polygons = PolygonParser.Parse(File.ReadAllText(polygonPath.ToString()));
        return Polygooner.GenerateGrid(size: 0.1, polygons);
    }

    private static List<City> InitCities(FileInfo citiesPath)
    {
        return [.. File.ReadAllLines(citiesPath.ToString()).Skip(1).Select(line =>
        {
            var parts = line.Split(',');
            var name = parts[0];
            var longitude = double.Parse(parts[2]);
            var latitude = double.Parse(parts[3]);
            var population = int.Parse(parts[1]);
            return new City(name, new Position(longitude, latitude), population);
        })];
    }
}
