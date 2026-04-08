namespace Engine.Init;

using Engine.Cost;
using Engine.Metrics;
using Engine.StationFactory;

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
        var dataPath = new DirectoryInfo("../data");
        var outputPath = new DirectoryInfo("../Perkuet");

        return new EngineSettings
        {
            CostConfig = new CostWeights
            {
                EffectiveQueueSize = 0.5f,
                PathDeviation = 10,
                PriceSensitivity = 10,
                ExpectedWaitTime = 1,
                Urgency = 1,
            },
            RunId = Guid.NewGuid(),
            MetricsConfig = new MetricsConfig
            {
                BufferSize = 5000,
                OutputDirectory = outputPath,
                RecordCarSnapshots = true,
                RecordArrivals = true,
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
            IntervalToUpdateEVs = 5 * 60,
            BatteryIntervalForCheckUrgency = 0.05f,
            CurrentAmoutOfEVsInDenmark = 583320,
            ChargingStepSeconds = 60,
            SimulationEndTime = 10000 * 60,
            SnapshotInterval = 1000 * 60,
            EVDistributionWindowsSize = 1 * 60,
            EVSpawnFraction = 0.10f,
            EnergyPricesPath = new FileInfo(Path.Combine(dataPath.FullName, "energy_prices.csv")),
            OsrmPath = new FileInfo(Path.Combine(dataPath.FullName, "osrm/output.osrm")),
            CitiesPath = new FileInfo(Path.Combine(dataPath.FullName, "CityInfo.csv")),
            GridPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark_charging_locations.json")),
            StationsPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark_charging_locations.json")),
            PolygonPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark.polygon.json")),
        };
    }
}
