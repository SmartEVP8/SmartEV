namespace Engine.StationFactory;

/// <summary>
/// Options controlling deterministic station generation behaviour.
/// </summary>
public record StationFactoryOptions
{
    /// <summary>
    /// Gets a value indicating whether dual charging points may be generated.
    /// </summary>
    public bool UseDualChargingPoints { get; init; } = true;

    /// <summary>
    /// Gets the probability that a generated charging point is dual,
    /// when dual charging points are enabled.
    /// </summary>
    public double DualChargingPointProbability { get; init; } = 0.8;

    /// <summary>
    /// Gets the total number of chargers to be distributed across generated stations.
    /// A higher number results in more chargers per station on average.
    /// </summary>
    public int TotalChargers { get; init; } = 10000;

    /// <summary>
    /// Gets the maximum power in kilowatts that a charger can deliver, which affects how quickly EVs can charge and how long they spend at charging stations.
    /// </summary>
    public ushort MaxPowerKW { get; init; } = 400;
}
