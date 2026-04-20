namespace Engine.Init;

using Engine.Cost;
using Engine.Metrics;
using Engine.StationFactory;
using System.Reflection;
using Core.Helper;

/// <summary>
/// Factory for creating EngineSettings with default configuration.
/// Centralizes engine configuration to avoid duplication across consumers.
/// </summary>
public static class EngineConfiguration
{
    /// <summary>
    /// Creates default EngineSettings with the standard workspace configuration.
    /// </summary>
    /// <returns>A configured EngineSettings instance.</returns>
    public static EngineSettings CreateDefaultSettings()
    {
        var dataPath = FindDataDirectory();
        var outputPath = new DirectoryInfo(Path.Combine(dataPath.Parent!.FullName, "Headless", "Perkuet"));

        return new EngineSettings
        {
            Seed = new Random(42),
            CostConfig = new CostWeights
            {
                EffectiveQueueSize = 1,
                PathDeviation = 0.8f,
                PriceSensitivity = 0.4f,
                ExpectedWaitTime = 1,
                Urgency = 0.5f,
            },
            RunId = Guid.NewGuid(),
            MetricsConfig = new MetricsConfig
            {
                BufferSize = 3000,
                OutputDirectory = outputPath,
                RecordArrivals = true,
                RecordEVWaitTimeInQueue = true,
                RecordStationSnapshots = true,
                RecordChargerSnapshots = true,
            },
            StationFactoryOptions = new StationFactoryOptions
            {
                UseDualChargingPoints = true,
                DualChargingPointProbability = 0.5,
                TotalChargers = 10000,
            },
            CurrentAmountOfEVsInDenmark = 583320, // Based on the number of registered EVs in Denmark as of 2026-03-22 https://mobility.dk/nyheder/nu-koerer-hver-femte-personbil-i-danmark-paa-el/
            ChargingStepSeconds = 60 * 1000,
            SimulationEndTime = 10000 * 60 * 1000,
            SnapshotInterval = 1000 * 20 * 60,
            EVDistributionWindowsSize = 1 * 60 * 1000,
            EVSpawnFraction = 0.10f,
            PopulationScaler = 0.7f,
            DistanceScaler = 1.7f,
            GridSize = 0.025f,
            EnergyPricesPath = new FileInfo(Path.Combine(dataPath.FullName, "energy_prices.csv")),
            OsrmPath = new FileInfo(Path.Combine(dataPath.FullName, "osrm/output.osrm")),
            CitiesPath = new FileInfo(Path.Combine(dataPath.FullName, "CityInfo.csv")),
            GridPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark_charging_locations.json")),
            StationsPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark_charging_locations.json")),
            PolygonPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark_polygon.json")),
            WetPolygonPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark_wet_polygon.json")),
        };
    }

    private static DirectoryInfo FindDataDirectory()
    {
        var searchDir = new DirectoryInfo(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory());

        while (searchDir != null)
        {
            var dataDir = new DirectoryInfo(Path.Combine(searchDir.FullName, "data"));
            if (dataDir.Exists)
                return dataDir;

            searchDir = searchDir.Parent;
        }

        throw Log.Error(0, 0, new DirectoryNotFoundException("Could not find 'data' directory in project hierarchy"));
    }
}
