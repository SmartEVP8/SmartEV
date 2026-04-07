namespace Headless;

using Engine;
using Engine.Cost;
using Engine.Events;
using Engine.Grid;
using Engine.Init;
using Engine.Metrics;
using Engine.Routing;
using Engine.Spawning;
using Engine.Services;
using Engine.StationFactory;
using Engine.Vehicles;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The entry point for the headless execution of the Engine. Initializes all necessary services and starts the simulation.
/// </summary>
public static class Program
{
    /// <summary>
    /// The main method initializes the Engine with the required services and configurations, then starts the simulation by resolving necessary services from the dependency injection.
    /// </summary>
    /// <returns>The running simulation.</returns>
    public static async Task Main()
    {
        var dataPath = new DirectoryInfo("data/");
        var logDirectory = new DirectoryInfo("logs");
        logDirectory.Create();

        var logPath = Path.Combine(logDirectory.FullName, $"headless-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        await using var logStream = new StreamWriter(File.Open(logPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true,
        };

        Console.SetOut(logStream);
        Console.SetError(logStream);

        var services = new ServiceCollection();
        var settings = new EngineSettings
        {
            CostConfig = new CostWeights
            {
                EffectiveQueueSize = 1,
                PathDeviation = 0.8f,
                PriceSensitivity = 0.4f,
                AvailableChargerRatio = 1,
                ExpectedWaitTime = 1,
                Urgency = 0.5f,
            },

            RunId = Guid.NewGuid(),

            MetricsConfig = new MetricsConfig
            {
                BufferSize = 5000,
                OutputDirectory = new DirectoryInfo("Perkuet"),
                RecordCarSnapshots = true,
                RecordArrivals = true,
                RecordStationSnapshots = true,
                RecordChargerSnapshots = true,
            },

            Seed = new Random(42),

            StationFactoryOptions = new StationFactoryOptions
            {
                UseDualChargingPoints = true,
                AllowMultiSocketChargers = true,
                DualChargingPointProbability = 0.5,
                MultiSocketChargerProbability = 1,
                TotalChargers = 10000,
            },

            CurrentAmoutOfEVsInDenmark = 583320, // Based on the number of registered EVs in Denmark as of 2026-03-22 https://mobility.dk/nyheder/nu-koerer-hver-femte-personbil-i-danmark-paa-el/

            ChargingStepSeconds = 60,

            SimulationEndTime = 100 * 60,

            SnapshotInterval = 20 * 60,

            EVDistributionWindowsSize = 1 * 60,

            EVSpawnFraction = 0.1f,

            PopulationScaler = 0.7f,

            DistanceScaler = 1.7f,

            EnergyPricesPath = new FileInfo(Path.Combine(dataPath.FullName, "energy_prices.csv")),
            OsrmPath = new FileInfo(Path.Combine(dataPath.FullName, "osrm/output.osrm")),
            CitiesPath = new FileInfo(Path.Combine(dataPath.FullName, "CityInfo.csv")),
            GridPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark_charging_locations.json")),
            StationsPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark_charging_locations.json")),
            PolygonPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark.polygon.json")),
        };

        services.AddSingleton(settings);
        Init.InitEngine(services);
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<EventScheduler>();
        provider.GetRequiredService<IOSRMRouter>();
        provider.GetRequiredService<ICostStore>();
        provider.GetRequiredService<MetricsService>();
        provider.GetRequiredService<EVFactory>();
        provider.GetRequiredService<SpatialGrid>();
        provider.GetRequiredService<IJourneySamplerProvider>();
        provider.GetRequiredService<StationService>();

        var coordinator = provider.GetRequiredService<Simulation>();
        await coordinator.Run();
    }
}
