namespace Engine.Init;

using Engine.Cost;
using Engine.Metrics;
using Engine.StationFactory;

/// <summary>
/// Represents the settings for the engine.
/// </summary>
public class EngineSettings
{
    /// <summary>
    /// Gets the cost configuration for the engine, which includes various weights and parameters.
    /// </summary>
    required public CostWeights CostConfig { get; init; }

    /// <summary>
    /// Gets the unique identifier for the simulation run.
    /// </summary>
    required public Guid RunId { get; init; }

    /// <summary>
    /// Gets the metrics configuration for the engine.
    /// </summary>
    required public MetricsConfig MetricsConfig { get; init; }

    /// <summary>
    /// Gets the random seed for the engine.
    /// </summary>
    required public Random Seed { get; init; }

    /// <summary>
    /// Gets the station factory options for the engine.
    /// </summary>
    required public StationFactoryOptions StationFactoryOptions { get; init; }

    /// <summary>
    /// Gets the current amount of EVs in Denmark.
    /// </summary>
    required public int CurrentAmoutOfEVsInDenmark { get; init; }

    /// <summary>
    /// Gets the interval at which the engine checks for urgent EVs.
    /// </summary>
    required public int IntervalToCheckUrgency { get; init; }

    /// <summary>
    /// Gets the file paths for various input data required by the engine inclduing energy prices.
    /// </summary>
    required public FileInfo EnergyPricesPath { get; init; }

    /// <summary>
    /// Gets the file paths for various input data required by the engine including OSRM data.
    /// </summary>
    required public FileInfo OsrmPath { get; init; }

    /// <summary>
    /// Gets the file paths for various input data required by the engine including city data.
    /// </summary>
    required public FileInfo CitiesPath { get; init; }

    /// <summary>
    /// Gets the file paths for various input data required by the engine, including grid, stations, and polygon data.
    /// </summary>
    required public FileInfo GridPath { get; init; }

    /// <summary>
    /// Gets the file path for Stations data required by the engine.
    /// </summary>
    required public FileInfo StationsPath { get; init; }

    /// <summary>
    /// Gets the file path for Polygon data required by the engine.
    /// </summary>
    required public FileInfo PolygonPath { get; init; }
}
