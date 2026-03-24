namespace Engine.Metrics.Snapshots;

using Core.Shared;

/// <summary>
/// Records metrics related to reservation requests made by EVs to charging stations.
/// </summary>
public class ReservationMetric
{
    /// <summary>
    /// Gets or sets the total number of reservation requests handled.
    /// </summary>
    public int TotalRequests { get; set; }

    /// <summary>
    /// Gets a dictionary mapping EV IDs to the time at which their reservation request was made.
    /// </summary>
    public Dictionary<int, Time> RequestTimestamps { get; } = [];

    /// <summary>
    /// Gets a dictionary mapping EV IDs to the detour deviation incurred by routing through the requested station.
    /// </summary>
    public Dictionary<int, float> PathDeviations { get; } = [];
}