namespace Engine.Init;

using Engine.Cost;
using Engine.Metrics;
using Engine.StationFactory;

public class EngineSettings
{
    required public CostWeights CostConfig { get; init; }
    required public Guid RunId { get; init; }
    required public MetricsConfig MetricsConfig { get; init; }
    required public Random Seed { get; init; }
    required public StationFactoryOptions StationFactoryOptions { get; init; }

    required public int MaximumEVs { get; init; }
    required public int CurrentAmoutOfEVsInDenmark { get; init; }
    required public int IntervalToCheckUrgency { get; init; }
    required public uint ChargingStepSeconds { get; init; }

    required public FileInfo EnergyPricesPath { get; init; }
    required public FileInfo OsrmPath { get; init; }
    required public FileInfo CitiesPath { get; init; }
    required public FileInfo GridPath { get; init; }
    required public FileInfo StationsPath { get; init; }
    required public FileInfo PolygonPath { get; init; }
}
