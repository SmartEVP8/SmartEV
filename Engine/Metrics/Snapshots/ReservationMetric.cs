namespace Engine.Metrics.Snapshots;

/// <summary>
/// Collects snapshots of reservation and cancellation requests made during the simulation.
/// </summary>
public class ReservationMetric
{
    /// <summary>
    /// Gets the list of reservation request snapshots.
    /// </summary>
    public List<ReservationSnapshot> Reservations { get; } = [];
}