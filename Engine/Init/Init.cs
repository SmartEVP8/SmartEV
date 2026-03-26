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
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Initializes the Engine by setting up all necessary services and configurations.
/// </summary>
public static class Init
{
    /// <summary>
    /// Initializes the Engine with the required services and configurations.
    /// </summary>
    /// <param name="services">The service collection to initialize.</param>
    public static void InitEngine(IServiceCollection services)
    {
        services.AddSingleton<IOSRMRouter>(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            return new OSRMRouter(settings.OsrmPath);
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            var energyPrices = sp.GetRequiredService<EnergyPrices>();
            var seed = settings.Seed;
            var stationPath = settings.StationsPath;
            var stationFactory = new StationFactory(settings.StationFactoryOptions, seed, energyPrices, stationPath);
            return stationFactory.CreateStations();
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            return new EVStore(settings.CurrentAmoutOfEVsInDenmark);
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
            var path = settings.EnergyPricesPath;
            var random = settings.Seed;
            return new EnergyPrices(path, random);
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
            var eventScheduler = sp.GetRequiredService<EventScheduler>();
            var evStore = sp.GetRequiredService<EVStore>();
            var settings = sp.GetRequiredService<EngineSettings>();
            var random = settings.Seed;
            return new CheckUrgencyHandler(eventScheduler, evStore, random);
        });

        services.AddSingleton(sp =>
        {
            var eventScheduler = sp.GetRequiredService<EventScheduler>();
            var evStore = sp.GetRequiredService<EVStore>();
            var settings = sp.GetRequiredService<EngineSettings>();
            var intervalSize = settings.IntervalToUpdateEVs;
            var urgencyInterval = settings.BatteryIntervalForCheckUrgency;
            return new CheckAndUpdateAllEVsHandler(eventScheduler, evStore, intervalSize, urgencyInterval);
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            var steps = settings.ChargingStepSeconds;
            return new ChargingIntegrator(steps);
        });

        services.AddSingleton(sp =>
        {
            var stations = sp.GetRequiredService<Dictionary<ushort, Station>>();
            var integrator = sp.GetRequiredService<ChargingIntegrator>();
            var scheduler = sp.GetRequiredService<EventScheduler>();
            var evStore = sp.GetRequiredService<EVStore>();
            var applyNewPath = sp.GetRequiredService<ApplyNewPath>();
            var settings = sp.GetRequiredService<EngineSettings>();
            var random = settings.Seed;
            return new StationService(stations.Values, integrator, scheduler, evStore, applyNewPath, random, settings);
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
