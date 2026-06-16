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
using Engine.Metrics.Snapshots;
using Engine.Utils;
using Core.Vehicles;

/// <summary>
/// Initializes the Engine by setting up all necessary services and configurations.
/// </summary>
public static class Init
{
    /// <summary>
    /// Initializes the Engine with the required services and configurations.
    /// </summary>
    /// <param name="services">The service collection to initialize.</param>
    /// <param name="settings">The engine settings used to configure services.</param>
    public static void InitEngine(IServiceCollection services, EngineSettings settings)
    {
        if (settings.EnablePerformanceMetrics)
            services.AddSingleton(new PerformanceMetrics());

        var polygons = InitPolygons(settings.PolygonPath);
        var wetPolygons = InitWetPolygons(settings.WetPolygonPath);
        var cities = InitCities(settings.CitiesPath);
        var spawnGrid = InitSpawnGrid(polygons, wetPolygons, settings.StationsPath, settings.GridSize);

        services.AddSingleton(_ => new EventScheduler());
        services.AddSingleton(sp =>
        {
            return new Dictionary<int, EV>(settings.CurrentAmountOfEVsInDenmark);
        });

        services.AddSingleton(sp =>
        {
            var path = settings.EnergyPricesPath;
            var random = settings.Seed;
            return new EnergyPrices(path, random);
        });

        services.AddSingleton<ICostStore>(sp =>
        {
            return new CostStore(settings.CostConfig);
        });

        services.AddSingleton(sp =>
        {
            var steps = settings.ChargingStepSeconds;
            return new ChargingIntegrator(steps);
        });

        services.AddSingleton(sp => new Dictionary<ushort, Station>(sp.GetRequiredService<List<Station>>().ToDictionary(s => s.Id)));

        services.AddSingleton(sp =>
        {
            var metricsConfig = settings.MetricsConfig;
            var runId = settings.RunId;
            return new MetricsService(metricsConfig, runId);
        });

        services.AddSingleton(sp =>
        {
            var highwayPolylines = HighwayFinder.GetHighwayNodes(settings.HighwayPolylinesPath);
            var energyPrices = sp.GetRequiredService<EnergyPrices>();
            var stationSeed = settings.StationSeed;
            var stationPath = settings.StationsPath;
            var stationFactory = new StationFactory(settings.StationFactoryOptions, stationSeed, energyPrices, stationPath, highwayPolylines);
            return stationFactory.CreateStations();
        });

        services.AddSingleton<IOSRMRouter>(sp =>
        {
            var stations = sp.GetRequiredService<List<Station>>();
            return new OSRMRouter(settings.OsrmPath, stations);
        });

        services.AddSingleton<IJourneySamplerProvider>(sp =>
        {
            var router = sp.GetRequiredService<IOSRMRouter>();
            var journeyPipeline = new JourneyPipeline(spawnGrid, cities, router);
            return new JourneySamplerProvider(journeyPipeline, (float)settings.DistanceScaler, wetPolygons);
        });

        services.AddSingleton(sp =>
        {
            var journeySamplerProvider = sp.GetRequiredService<IJourneySamplerProvider>();
            var router = sp.GetRequiredService<IOSRMRouter>();
            var random = settings.Seed;
            var options = new EVOptions();
            return new EVFactory(random, journeySamplerProvider, router, options);
        });

        services.AddSingleton(sp =>
        {
            var stations = sp.GetRequiredService<Dictionary<ushort, Station>>();
            return new SpatialGrid(spawnGrid, stations);
        });

        services.AddSingleton(sp =>
        {
            var stations = sp.GetRequiredService<Dictionary<ushort, Station>>();
            var integrator = sp.GetRequiredService<ChargingIntegrator>();
            var scheduler = sp.GetRequiredService<EventScheduler>();
            var evs = sp.GetRequiredService<Dictionary<int, EV>>();
            var metrics = sp.GetRequiredService<MetricsService>();
            return new StationService(stations.Values, integrator, scheduler, evs, metrics);
        });

        services.AddSingleton(sp =>
        {
            var router = sp.GetRequiredService<IOSRMRouter>();
            var stations = sp.GetRequiredService<Dictionary<ushort, Station>>();
            var grid = sp.GetRequiredService<SpatialGrid>();
            var stationService = sp.GetRequiredService<StationService>();
            var chargerBufferPercent = settings.ChargeBufferPercent;
            return new FindCandidateStationService(router, stations, grid, stationService, chargerBufferPercent, settings.ProcessorCount);
        });

        services.AddSingleton(sp =>
        {
            var router = sp.GetRequiredService<IOSRMRouter>();
            return new EVDetourPlanner(router);
        });

        services.AddSingleton(sp =>
        {
            var evFactory = sp.GetRequiredService<EVFactory>();
            var evs = sp.GetRequiredService<Dictionary<int, EV>>();
            var eventScheduler = sp.GetRequiredService<EventScheduler>();
            return new EVPopulator(evFactory, evs, eventScheduler);
        });

        services.AddSingleton(sp =>
        {
            var evPopulator = sp.GetRequiredService<EVPopulator>();
            var scheduler = sp.GetRequiredService<EventScheduler>();
            var journeySampler = sp.GetRequiredService<IJourneySamplerProvider>();
            var distributionWindow = settings.EVDistributionWindowsSize;
            var spawnFraction = settings.EVSpawnFraction;
            return new EVService(evPopulator, scheduler, distributionWindow, journeySampler, spawnFraction);
        });

        services.AddSingleton(sp =>
        {
            var snapshotInterval = settings.SnapshotInterval;
            var metrics = sp.GetRequiredService<MetricsService>();
            var stations = sp.GetRequiredService<List<Station>>();
            var stationService = sp.GetRequiredService<StationService>();
            var stationMetricsCollector = new StationMetricsCollector(stations, stationService);
            var scheduler = sp.GetRequiredService<EventScheduler>();
            return new SnapshotEventHandler(snapshotInterval, metrics, stationMetricsCollector, scheduler);
        });

        services.AddSingleton(sp =>
        {
            var metrics = sp.GetRequiredService<MetricsService>();
            var evs = sp.GetRequiredService<Dictionary<int, EV>>();
            return new DestinationArrivalHandler(metrics, evs);
        });

        services.AddSingleton(sp =>
        {
            var costStore = sp.GetRequiredService<ICostStore>();
            var stationService = sp.GetRequiredService<StationService>();
            var energyPrices = sp.GetRequiredService<EnergyPrices>();
            return new CostFunction(costStore, stationService, energyPrices);
        });

        services.AddSingleton(sp =>
        {
            var findCandidateStationService = sp.GetRequiredService<FindCandidateStationService>();
            var computeCost = sp.GetRequiredService<CostFunction>();
            var scheduler = sp.GetRequiredService<EventScheduler>();
            var applyNewPath = sp.GetRequiredService<EVDetourPlanner>();
            var stationService = sp.GetRequiredService<StationService>();
            var metrics = sp.GetService<PerformanceMetrics>();
            return new FindCandidateStationsHandler(findCandidateStationService, computeCost, scheduler, applyNewPath, stationService, metrics);
        });

        services.AddSingleton(sp =>
        {
            var stationService = sp.GetRequiredService<StationService>();
            var snapshotHandler = sp.GetRequiredService<SnapshotEventHandler>();
            var evService = sp.GetRequiredService<EVService>();
            var destinationArrivalHandler = sp.GetRequiredService<DestinationArrivalHandler>();
            var findCandidateStationsHandler = sp.GetRequiredService<FindCandidateStationsHandler>();
            var eventSubscriber = sp.GetService<IEngineEventSubscriber>();
            var metrics = sp.GetService<PerformanceMetrics>();
            return new EventDispatcher(stationService, snapshotHandler, findCandidateStationsHandler, evService, destinationArrivalHandler, eventSubscriber, metrics);
        });

        services.AddSingleton(sp =>
        {
            var scheduler = sp.GetRequiredService<EventScheduler>();
            var findCandidateStationService = sp.GetRequiredService<FindCandidateStationService>();

            scheduler.RegisterPreProcessor<FindCandidateStations>(
                findCandidateStationService.PreComputeCandidateStation());

            return new Simulation(
                sp.GetRequiredService<EventDispatcher>(),
                scheduler,
                settings.SimulationStartTime,
                settings.SimulationEndTime);
        });
    }

    private static SpawnGrid InitSpawnGrid(
        List<List<Position>> polygons,
        List<List<Position>> wetPolygons,
        FileInfo stationsPath,
        double size)
    {
        var stations = StationParser.Parse(File.ReadAllText(stationsPath.ToString()));

        return Polygooner.GenerateGrid(size, polygons, wetPolygons, stations);
    }

    private static List<List<Position>> InitPolygons(FileInfo polygonPath)
    {
        return PolygonParser.Parse(File.ReadAllText(polygonPath.ToString()));
    }

    private static List<List<Position>> InitWetPolygons(FileInfo wetPolygonPath)
    {
        return PolygonParser.Parse(File.ReadAllText(wetPolygonPath.ToString()));
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
