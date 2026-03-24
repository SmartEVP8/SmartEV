namespace Engine.Init;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Shared;
using Engine.Cost;
using Engine.Events;
using Engine.Grid;
using Engine.Metrics;
using Engine.Parsers;
using Engine.Routing;
using Engine.Spawning;
using Engine.StationFactory;
using Engine.Services;
using Engine.Vehicles;
using Engine.Metrics.Snapshots;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Initializes the SmartEV simulation engine by registering all services with the dependency injection container.
/// </summary>
public static class Init
{
    /// <summary>
    /// Registers all Engine services and their dependencies with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to register services into.</param>
    public static void InitEngine(IServiceCollection services)
    {
        services.AddSingleton<IOSRMRouter>(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            return new OSRMRouter(settings.OsrmPath);
        });

        services.AddSingleton<EventScheduler>();
        
        services.AddSingleton<ChargingIntegrator>();
        
        services.AddSingleton<ReservationMetric>();

        services.AddSingleton(sp =>
        {
            var router = sp.GetRequiredService<IOSRMRouter>();
            return new PathDeviator(router);
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
            return stationFactory.CreateStations(settings.StationsPath);
        });

        services.AddSingleton<IJourneySamplerProvider>(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            var router = sp.GetRequiredService<IOSRMRouter>();
            var spawnGrid = InitSpawnGrid(settings.PolygonPath);
            var cities = InitCities(settings.CitiesPath);
            var journeyPipeline = new JourneyPipeline(spawnGrid, cities, router);
            return new JourneySamplerProvider(journeyPipeline);
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
            var journeySamplerProvider = sp.GetRequiredService<IJourneySamplerProvider>();
            var router = sp.GetRequiredService<IOSRMRouter>();
            var random = settings.Seed;
            return new EVFactory(random, journeySamplerProvider, router);
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            var stations = sp.GetRequiredService<Dictionary<ushort, Station>>();
            var spawnGrid = InitSpawnGrid(settings.PolygonPath);
            return new SpatialGrid(spawnGrid, stations);
        });

        services.AddSingleton(sp =>
        {
            var stations = sp.GetRequiredService<Dictionary<ushort, Station>>();
            var integrator = sp.GetRequiredService<ChargingIntegrator>();
            var scheduler = sp.GetRequiredService<EventScheduler>();
            var pathDeviator = sp.GetRequiredService<PathDeviator>();
            var metrics = sp.GetRequiredService<ReservationMetric>();
            return new StationService(stations.Values, integrator, scheduler, pathDeviator, metrics);
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
