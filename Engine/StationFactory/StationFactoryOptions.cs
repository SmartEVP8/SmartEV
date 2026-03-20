namespace Engine.StationFactory;

using Core.Shared;

/// <summary>
/// Options controlling deterministic station generation behaviour.
/// </summary>
public class StationFactoryOptions
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
    /// Gets a value indicating whether chargers that support multiple socket types may be generated.
    /// </summary> 
    public bool AllowMultiSocketChargers { get; init; } = true;

    /// <summary>
    /// Gets the probability that a generated charger supports more than one socket type.
    /// </summary>
    public double MultiSocketChargerProbability { get; init; } = 0.2;

    /// <summary>
    /// Gets the total number of chargers to be distributed across generated stations.
    /// A higher number results in more chargers per station on average.
    /// </summary>
    public int TotalChargers { get; init; } = 10000;

    /// <summary>
    /// Gets a dictionary mapping each socket type to its probability of occurrence in the generated stations.
    /// The probabilities should sum to 1.0 across all socket types.
    /// </summary>
    public Dictionary<Socket, double> SocketProbabilities { get; init; } = new()
    {
        { Socket.CHADEMO, 0.023 },
        { Socket.CCS2, 0.204 },
        { Socket.Type2SocketOnly, 0.626 },
        { Socket.Type2Tethered, 0.144 },
        { Socket.NACS, 0.0019 },
    };
}