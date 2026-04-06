namespace Engine.Metrics.Snapshots;

using Core.Charging;
using Core.Shared;

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
    /// Gets the total power delivered across all chargers in kW.
    /// </summary>
    required public float TotalDeliveredKWh { get; init; }

    /// <summary>
    /// Gets the total maximum power capacity across all chargers in kW.
    /// </summary>
    required public float TotalMaxKWh { get; init; }

    /// <summary>
    /// Gets the total number of EVs queued across all chargers at snapshot time.
    /// </summary>
    required public int TotalQueueSize { get; init; }

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
}
