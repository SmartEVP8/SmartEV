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
    /// Gets the probability distribution for charger power outputs in kW.
    /// The probabilities should sum to 1.
    /// </summary>
    /// <remarks>
    /// These probabilites are based on our dataset. The probabilities are calculated on only the amount of chargers
    /// with over 100 kW power output so all stations with less have been excluded. 
    /// </remarks>
    public IReadOnlyDictionary<ushort, double> PowerOutputProbabilitiesKW { get; init; }
        = new Dictionary<ushort, double>
        {
            { 120, 0.0085 },
            { 150, 0.1010 },
            { 400, 0.8905 },
        };
}