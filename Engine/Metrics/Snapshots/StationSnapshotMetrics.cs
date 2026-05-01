namespace Engine.Metrics.Snapshots;

/// <summary>
/// A point-in-time snapshot of a single station's metrics.
/// Collected once per simulation hour.
/// </summary>
public record StationSnapshotMetric
{
    /// <summary>
    /// Gets the simulation timestamp (seconds) when this snapshot was taken.
    /// </summary>
    required public uint SimTime { get; init; }

    /// <summary>
    /// Gets the station this snapshot was taken from.
    /// </summary>
    required public ushort StationId { get; init; }

    /// <summary>
    /// Gets the station's energy price in DKK/kWh at snapshot time.
    /// </summary>
    required public float Price { get; init; }

    /// <summary>
    /// Gets the total number of chargers at the station.
    /// </summary>
    required public int TotalChargers { get; init; }

    /// <summary>
    /// Gets the number of reservations made to this station since the last snapshot.
    /// </summary>
    required public uint Reservations { get; init; }

    /// <summary>
    /// Gets the number of cancellations made to this station since the last snapshot.
    /// </summary>
    required public uint Cancellations { get; init; }

    /// <summary>
    /// Gets the expected wait time in milliseconds for an EV arriving at this station at snapshot time, based on current reservations and charger availability.
    /// </summary>
    required public uint ExpectedWaitTimeMiliseconds { get; init; }
}
