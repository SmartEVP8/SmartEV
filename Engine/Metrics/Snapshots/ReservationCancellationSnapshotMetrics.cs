namespace Engine.Metrics.Snapshots;

using Core.Shared;

/// <summary>
/// A point-in-time snapshot of reservation metrics, collected once per simulation time window.
/// </summary>
public readonly struct ReservationCancellationSnapshotMetric
{
    /// <summary>
    /// Gets the simulation timestamp when this snapshot was taken.
    /// </summary>
    required public Time SimTime { get; init; }

    /// <summary>
    /// Gets the total number of reservations cancelled across all stations in the last time window.
    /// </summary>
    required public int TotalReservationCancellations { get; init; }
}