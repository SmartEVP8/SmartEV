namespace Engine.Init;

using Core.Shared;
using Engine.Cost;
using Engine.Metrics;
using Engine.StationFactory;

/// <summary>
/// Represents the settings for the engine.
/// </summary>
public record EngineSettings
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
    required public int CurrentAmountOfEVsInDenmark { get; init; }

    /// <summary>
    /// Gets the number of seconds an EV is expected to spend charging at a station, used for simulating charging time and scheduling.
    /// </summary>
    required public uint ChargingStepSeconds { get; init; }

    /// <summary>
    ///   Gets the interval at which snapshots of the simulation state are taken.
    /// </summary>
    required public uint SnapshotInterval { get; init; }

    /// <summary>
    /// Gets a value indicating whether performance metrics should be collected and displayed.
    /// </summary>
    required public bool EnablePerformanceMetrics { get; init; } = true;

    /// <summary>
    ///  Gets the size of the grid cells.
    /// </summary>
    required public double GridSize { get; init; }

    /// <summary>
    /// Gets a ChargeBufferPercent, which is a buffer percentage applied to the target SoC when determining if a station is a viable candidate for charging.
    /// </summary>
    required public float ChargeBufferPercent { get; init; }

    /// <summary>
    /// Gets the simulation end time, which determines
    /// when the simulation should stop running.
    /// </summary>
    required public Time SimulationEndTime { get; init; } = Time.MillisecondsPerDay * 7;

    /// <summary>
    /// Gets the simulation start time,
    /// which determines when the simulation starts.
    /// </summary>
    required public Time SimulationStartTime { get; init; } = Time.MillisecondsPerDay;

    /// <summary>
    /// Gets the size of the windows in which EVs are spawned, which affects the distribution of EV arrivals over time.
    /// </summary>
    required public Time EVDistributionWindowsSize { get; init; }

    /// <summary>
    /// Gets the fraction of the total EV population that should be spawned in the simulation.
    /// </summary>
    required public double EVSpawnFraction { get; init; }

    /// <summary>
    /// Gets the population scalar used in the gravity model for journey sampling, which controls how population size affects the probability of selecting a destination.
    /// </summary>
    required public double PopulationScaler { get; init; }

    /// <summary>
    /// Gets the distance scalar used in the gravity model for journey sampling, which controls how distance affects the probability of selecting a destination.
    /// </summary>
    required public double DistanceScaler { get; init; }

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

    /// <summary>
    /// Gets the file path for WetPolygon data required by the engine, which is used for determining where EVs cannot be spawned.
    /// </summary>
    required public FileInfo WetPolygonPath { get; init; }
}
