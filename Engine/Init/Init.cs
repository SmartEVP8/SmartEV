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
using Engine.Events.Middleware;

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
        services.AddSingleton(sp =>
        {
            return new EventScheduler([]);
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
            return new Dictionary<ushort, Station>(sp.GetRequiredService<List<Station>>().ToDictionary(s => s.Id));
        });

        services.AddSingleton<IOSRMRouter>(sp =>
     {
         var settings = sp.GetRequiredService<EngineSettings>();
         var stations = sp.GetRequiredService<List<Station>>();
         return new OSRMRouter(settings.OsrmPath, stations);
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
            return new StationService(stations.Values, integrator, scheduler, evStore, applyNewPath);
        });

        services.AddSingleton(sp =>
        {
            var evFactory = sp.GetRequiredService<EVFactory>();
            var evStore = sp.GetRequiredService<EVStore>();
            var eventScheduler = sp.GetRequiredService<EventScheduler>();
            return new EVPopulator(evFactory, evStore, eventScheduler);
        });

        services.AddSingleton(sp =>
        {
            var evPopulator = sp.GetRequiredService<EVPopulator>();
            var scheduler = sp.GetRequiredService<EventScheduler>();
            var settings = sp.GetRequiredService<EngineSettings>();
            var distributionWindow = settings.EVDistributionWindowsSize;
            var spawnFraction = settings.EVSpawnFraction;
            return new EVService(evPopulator, scheduler, distributionWindow, spawnFraction);
        });

        services.AddSingleton(sp =>
        {
            var scheduler = sp.GetRequiredService<EventScheduler>();
            var metrics = sp.GetRequiredService<MetricsService>();
            var settings = sp.GetRequiredService<EngineSettings>();
            var snapshotInterval = settings.SnapshotInterval;
            var stations = sp.GetRequiredService<Dictionary<ushort, Station>>();
            return new SnapshotEventHandler(snapshotInterval, DateTimeOffset.UtcNow, stations, metrics, scheduler); // TODO: Look into how we can remove DateTime
        });

        services.AddSingleton(sp =>
        {
            var stationService = sp.GetRequiredService<StationService>();
            var checkUrgencyHandler = sp.GetRequiredService<CheckUrgencyHandler>();
            var snapshotHandler = sp.GetRequiredService<SnapshotEventHandler>();
            return new CheckAndUpdateAllEVsHandler(sp.GetRequiredService<EventScheduler>(), sp.GetRequiredService<EVStore>(), sp.GetRequiredService<EngineSettings>().IntervalToUpdateEVs, sp.GetRequiredService<EngineSettings>().BatteryIntervalForCheckUrgency);
        });

        services.AddSingleton(sp =>
        {
            var metrics = sp.GetRequiredService<MetricsService>();
            var evstore = sp.GetRequiredService<EVStore>();
            return new DestinationArrivalHandler(metrics, evstore);
        });

        services.AddSingleton(sp =>
        {
            var costStore = sp.GetRequiredService<ICostStore>();
            return new ComputeCost(costStore);
        });

        services.AddSingleton(sp =>
        {
            var router = sp.GetRequiredService<IOSRMRouter>();
            var stations = sp.GetRequiredService<Dictionary<ushort, Station>>();
            var grid = sp.GetRequiredService<SpatialGrid>();
            var evStore = sp.GetRequiredService<EVStore>();
            return new FindCandidateStationService(router, stations, grid, evStore);
        });

        services.AddSingleton(sp =>
        {
            var findCandidateStationService = sp.GetRequiredService<FindCandidateStationService>();
            var computeCost = sp.GetRequiredService<ComputeCost>();
            var scheduler = sp.GetRequiredService<EventScheduler>();
            var evStore = sp.GetRequiredService<EVStore>();
            return new FindCandidateStationsHandler(findCandidateStationService, computeCost, scheduler, evStore);
        });

        services.AddSingleton(sp =>
        {
            var stationService = sp.GetRequiredService<StationService>();
            var checkUrgencyHandler = sp.GetRequiredService<CheckUrgencyHandler>();
            var snapshotHandler = sp.GetRequiredService<SnapshotEventHandler>();
            var evService = sp.GetRequiredService<EVService>();
            var checkAndUpdateAllEVsHandler = sp.GetRequiredService<CheckAndUpdateAllEVsHandler>();
            var destinationArrivalHandler = sp.GetRequiredService<DestinationArrivalHandler>();
            var findCandidateStationsHandler = sp.GetRequiredService<FindCandidateStationsHandler>();
            return new EventDispatcher(stationService, checkUrgencyHandler, snapshotHandler, findCandidateStationsHandler, evService, destinationArrivalHandler, checkAndUpdateAllEVsHandler);
        });

        services.AddSingleton(sp =>
        {
            var scheduler = sp.GetRequiredService<EventScheduler>();
            var dispatcher = sp.GetRequiredService<EventDispatcher>();
            var settings = sp.GetRequiredService<EngineSettings>();
            var simulationEndTime = settings.SimulationEndTime;
            var intervalToUpdateEVs = settings.IntervalToUpdateEVs;
            return new Simulation(dispatcher, scheduler, simulationEndTime);
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
