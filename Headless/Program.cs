namespace Headless;

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

public static class Program
{
    public static async Task Main()
    {
        var dataPath = new DirectoryInfo("../data/");
        var services = new ServiceCollection();
        var settings = new EngineSettings
        {
            CostConfig = new CostWeights
            {
                ExpectedQueueSize = 1,
                PathDeviation = 1,
                PriceSensitivity = 1,
            },

            RunId = Guid.NewGuid(),

            MetricsConfig = new MetricsConfig
            {
                BufferSize = 5000,
                OutputDirectory = new DirectoryInfo("Perkuet"),
                RecordCarSnapshots = true,
                RecordDeadlines = true,
                RecordStationSnapshots = true,
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

            // TODO: GET THE ACTUAL NUMBER
            MaximumEVs = 600_000,

            IntervalToCheckUrgency = 10,

            CurrentAmoutOfEVsInDenmark = 583320, // Based on the number of registered EVs in Denmark as of 2026-03-22 https://mobility.dk/nyheder/nu-koerer-hver-femte-personbil-i-danmark-paa-el/

            ChargingStepSeconds = 60,

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
    }
}
