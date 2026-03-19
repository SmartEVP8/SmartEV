using Engine.Vehicles;
using Core.Vehicles.Configs;
namespace Headless;

using Engine.Cost;
using Engine.Init;
using Engine.Metrics;
using Engine.StationFactory;
using Microsoft.Extensions.DependencyInjection;
using Engine;
using Engine.Events;
using Engine.Grid;
using Engine.Routing;
using Engine.Vehicles;

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
        provider.GetRequiredService<JourneyPipeline>();
    }

}
