namespace Engine.Init;

using Engine.Cost;
using Engine.Metrics;
using Engine.StationFactory;
using System.Globalization;
using System.Reflection;
using Serilog;
using Core.Shared;

/// <summary>
/// Factory for creating EngineSettings with default configuration.
/// Centralizes engine configuration to avoid duplication across consumers.
/// </summary>
public static class EngineConfiguration
{
    private const string _pythonModeEnvVar = "PYTHON_MODE";
    private const string _priceSensitivityEnvVar = "COST_WEIGHT_PRICE_SENSITIVITY";
    private const string _pathDeviationEnvVar = "COST_WEIGHT_PATH_DEVIATION";
    private const string _expectedWaitTimeEnvVar = "COST_WEIGHT_EXPECTED_WAIT_TIME";
    private const string _seedEnvVar = "ENGINE_SEED";
    private const string _simulationStartTimeEnvVar = "SIMULATION_START_TIME_MS";
    private const string _simulationEndTimeEnvVar = "SIMULATION_END_TIME_MS";

    /// <summary>
    /// Creates default EngineSettings with the standard workspace configuration.
    /// </summary>
    /// <returns>A configured EngineSettings instance.</returns>
    public static EngineSettings CreateDefaultSettings()
    {
        var dataPath = FindDataDirectory();
        var outputPath = new DirectoryInfo(Path.Combine(dataPath.Parent!.FullName, "Headless", "Perkuet"));
        var isPythonMode = Environment.GetEnvironmentVariable(_pythonModeEnvVar)?.ToLower() == "true";
        var seed = ReadIntFromEnvironment(_seedEnvVar, 42, isPythonMode);

        return new EngineSettings
        {
            Seed = new Random(seed),
            StationSeed = new Random(43),
            ProcessorCount = Environment.ProcessorCount,
            CostConfig = new CostWeights
            {
                PathDeviation = ReadWeightFromEnvironment(_pathDeviationEnvVar, 0.4f, 0f, 1f, isPythonMode),
                PriceSensitivity = ReadWeightFromEnvironment(_priceSensitivityEnvVar, 0.4f, 0f, 1f, isPythonMode),
                ExpectedWaitTime = ReadWeightFromEnvironment(_expectedWaitTimeEnvVar, 0.4f, 0f, 1f, isPythonMode),
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
                ChargerSeed = new Random(44),
            },
            CurrentAmountOfEVsInDenmark = 583320,
            ChargingStepSeconds = 60 * 1000,
            SimulationStartTime = (uint)ReadIntFromEnvironment(_simulationStartTimeEnvVar, (int)Time.MillisecondsPerDay, isPythonMode),
            SimulationEndTime = (uint)ReadIntFromEnvironment(_simulationEndTimeEnvVar, (int)Time.MillisecondsPerDay * 2, isPythonMode),
            SnapshotInterval = 1000 * 20 * 60,
            EnablePerformanceMetrics = true,
            EVDistributionWindowsSize = 30 * 60 * 1000,
            EVSpawnFraction = 0.3,
            PopulationScaler = 0.7f,
            ChargeBufferPercent = 0.9f,
            DistanceScaler = 1.7f,
            GridSize = 0.025,
            EnergyPricesPath = new FileInfo(Path.Combine(dataPath.FullName, "energy_prices.csv")),
            OsrmPath = new FileInfo(Path.Combine(dataPath.FullName, "osrm/output.osrm")),
            CitiesPath = new FileInfo(Path.Combine(dataPath.FullName, "CityInfo.csv")),
            GridPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark_charging_locations.json")),
            StationsPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark_charging_locations.json")),
            PolygonPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark_polygon.json")),
            WetPolygonPath = new FileInfo(Path.Combine(dataPath.FullName, "denmark_wet_polygon.json")),
            HighwayPolylinesPath = new FileInfo(Path.Combine(dataPath.FullName, "highway_polylines.json")),
        };
    }

    private static float ReadWeightFromEnvironment(string envVar, float defaultValue, float min, float max, bool isPythonMode)
    {
        var value = Environment.GetEnvironmentVariable(envVar);

        if (string.IsNullOrWhiteSpace(value))
            return HandleError($"Required environment variable {envVar} is missing.", new ArgumentException(), defaultValue);

        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return HandleError($"Environment variable {envVar}='{value}' is not a valid float.", new FormatException(), defaultValue);

        if (parsed < min || parsed > max)
            return HandleError($"{envVar}='{value}' is out of bounds [{min}, {max}].", new ArgumentOutOfRangeException(), Math.Clamp(parsed, min, max));

        return parsed;

        float HandleError(string message, Exception ex, float fallback)
        {
            if (isPythonMode)
            {
                Log.Error($"[Python Mode] {message}");
                throw ex;
            }

            Log.Information($"{message} Falling back to {fallback}.");
            return fallback;
        }
    }

    private static int ReadIntFromEnvironment(string envVar, int defaultValue, bool isPythonMode)
    {
        var value = Environment.GetEnvironmentVariable(envVar);

        if (string.IsNullOrWhiteSpace(value))
            return HandleError($"Required environment variable {envVar} is missing.", new ArgumentException(), defaultValue);

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return HandleError($"Environment variable {envVar}='{value}' is not a valid int.", new FormatException(), defaultValue);

        return parsed;

        int HandleError(string message, Exception ex, int fallback)
        {
            if (isPythonMode)
            {
                Log.Error($"[Python Mode] {message}");
                throw ex;
            }

            Log.Information($"{message} Falling back to {fallback}.");
            return fallback;
        }
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

        Log.Error("Could not find 'data' directory in project hierarchy. Searched from {@CurrentDirectory}", Directory.GetCurrentDirectory());
        throw new DirectoryNotFoundException("Could not find 'data' directory in project hierarchy. Searched from " + Directory.GetCurrentDirectory());
    }
}
