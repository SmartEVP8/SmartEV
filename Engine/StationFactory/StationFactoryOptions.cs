namespace Engine.StationFactory;

/// <summary>
/// Options controlling deterministic station generation behaviour.
/// </summary>
public class StationFactoryOptions
{
    /// <summary>
    /// Gets the seed used for deterministic station generation.
    /// The same seed produces the same stations.
    /// </summary>
    public int Seed { get; init; } = 123456;

    /// <summary>
    /// Gets a value indicating whether dual charging points may be generated.
    /// </summary>
    public bool UseDualChargingPoints { get; init; } = true;

    /// <summary>
    /// Gets the probability that a generated charging point is dual,
    /// when dual charging points are enabled.
    /// </summary>
    public double DualChargingPointProbability { get; init; } = 0.3;
}