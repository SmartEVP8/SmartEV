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

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            return new ChargingIntegrator(settings.ChargingStepSeconds);
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            return new EnergyPrices(settings.EnergyPricesPath, settings.Seed);
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            return new MetricsService(settings.MetricsConfig, settings.RunId);
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            var energyPrices = sp.GetRequiredService<EnergyPrices>();
            var stationFactory = new StationFactory(settings.StationFactoryOptions, settings.Seed, energyPrices, settings.StationsPath);
            return stationFactory.CreateStations();
        });

        services.AddSingleton(sp =>
        {
            // Now wrap that list into the Dictionary needed by other services
            var stationsList = sp.GetRequiredService<List<Station>>();
            return stationsList.ToDictionary(s => s.Id);
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
            var stations = sp.GetRequiredService<Dictionary<ushort, Station>>();
            var spawnGrid = InitSpawnGrid(settings.PolygonPath);
            return new SpatialGrid(spawnGrid, stations);
        });

        // 3. Routing & Cost Logic
        services.AddSingleton<ICostStore>(sp => (ICostStore)sp.GetRequiredService<EngineSettings>().CostConfig);
        services.AddSingleton(sp => new ComputeCost(sp.GetRequiredService<ICostStore>()));
        services.AddSingleton(sp => new ApplyNewPath(sp.GetRequiredService<IOSRMRouter>()));
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            return new SnapshotEventHandler(
                settings.SnapshotInterval,
                DateTimeOffset.UtcNow,
                sp.GetRequiredService<Dictionary<ushort, Station>>(),
                sp.GetRequiredService<EVStore>(),
                sp.GetRequiredService<MetricsService>(),
                sp.GetRequiredService<EventScheduler>());
        });

        services.AddSingleton(sp =>
        {
            var stations = sp.GetRequiredService<Dictionary<ushort, Station>>();
            return new StationService(
                stations.Values,
                sp.GetRequiredService<ChargingIntegrator>(),
                sp.GetRequiredService<EventScheduler>(),
                sp.GetRequiredService<EVStore>(),
                sp.GetRequiredService<ApplyNewPath>(),
                sp.GetRequiredService<MetricsService>(),
                sp.GetRequiredService<SnapshotEventHandler>());
        });

        services.AddSingleton<IJourneySamplerProvider>(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            var router = sp.GetRequiredService<IOSRMRouter>();
            var journeyPipeline = new JourneyPipeline(InitSpawnGrid(settings.PolygonPath), InitCities(settings.CitiesPath), router);
            return new JourneySamplerProvider(journeyPipeline);
        });

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            return new EVFactory(settings.Seed, sp.GetRequiredService<IJourneySamplerProvider>(), sp.GetRequiredService<IOSRMRouter>());
        });

        services.AddSingleton(sp => new EVPopulator(sp.GetRequiredService<EVFactory>(), sp.GetRequiredService<EVStore>(), sp.GetRequiredService<EventScheduler>()));

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            return new EVService(sp.GetRequiredService<EVPopulator>(), sp.GetRequiredService<EventScheduler>(), settings.EVDistributionWindowsSize, settings.EVSpawnFraction);
        });

        services.AddSingleton(sp => new DestinationArrivalHandler(sp.GetRequiredService<MetricsService>(), sp.GetRequiredService<EVStore>()));

        services.AddSingleton(sp => new CheckUrgencyHandler(sp.GetRequiredService<EventScheduler>(), sp.GetRequiredService<EVStore>(), sp.GetRequiredService<EngineSettings>().Seed));

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<EngineSettings>();
            return new CheckAndUpdateAllEVsHandler(sp.GetRequiredService<EventScheduler>(), sp.GetRequiredService<EVStore>(), settings.IntervalToUpdateEVs, settings.BatteryIntervalForCheckUrgency);
        });

        services.AddSingleton(sp => new FindCandidateStationService(sp.GetRequiredService<IOSRMRouter>(), sp.GetRequiredService<Dictionary<ushort, Station>>(), sp.GetRequiredService<SpatialGrid>(), sp.GetRequiredService<EVStore>()));

        services.AddSingleton(sp => new FindCandidateStationsHandler(sp.GetRequiredService<FindCandidateStationService>(), sp.GetRequiredService<ComputeCost>(), sp.GetRequiredService<EventScheduler>(), sp.GetRequiredService<EVStore>()));

        services.AddSingleton(sp => new EventDispatcher(
            sp.GetRequiredService<StationService>(),
            sp.GetRequiredService<CheckUrgencyHandler>(),
            sp.GetRequiredService<SnapshotEventHandler>(),
            sp.GetRequiredService<FindCandidateStationsHandler>(),
            sp.GetRequiredService<EVService>(),
            sp.GetRequiredService<DestinationArrivalHandler>(),
            sp.GetRequiredService<CheckAndUpdateAllEVsHandler>()));

        services.AddSingleton(sp => new Simulation(sp.GetRequiredService<EventDispatcher>(), sp.GetRequiredService<EventScheduler>(), sp.GetRequiredService<EngineSettings>().SimulationEndTime));
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