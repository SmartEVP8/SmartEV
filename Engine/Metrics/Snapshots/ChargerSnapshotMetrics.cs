namespace Engine.Metrics.Snapshots;

using Core.Charging;
using Core.Shared;

/// <summary>
/// A per-window snapshot of a single charger's metrics.
/// Collected once per snapshot interval.
/// </summary>
public record ChargerSnapshotMetric
{
    /// <summary>
    /// Gets the simulation timestamp in seconds when this snapshot was taken.
    /// </summary>
    required public uint SimTime { get; init; }

    /// <summary>
    /// Gets the station this charger belongs to.
    /// </summary>
    required public ushort StationId { get; init; }

    /// <summary>
    /// Gets the charger this snapshot was taken from.
    /// </summary>
    required public int ChargerId { get; init; }

    /// <summary>
    /// Gets the maximum power capacity of this charger in kWh.
    /// </summary>
    required public float MaxKWh { get; init; }

    /// <summary>
    /// Gets the maximum utilization of this charger observed during the snapshot window.
    /// Value is in range [0, 1].
    /// </summary>
    required public float Utilization { get; init; }

    /// <summary>
    /// Collects a snapshot from a charger at the given simulation time.
    /// </summary>
    /// <param name="charger">The charger to snapshot.</param>
    /// <param name="stationId">The id of the station that owns the charger.</param>
    /// <param name="simTime">The simulation timestamp when this snapshot is taken.</param>
    /// <param name="utilization">The charger utilization in range [0, 1].</param>
    /// <returns>A snapshot metric for the specified charger at the provided simulation time.</returns>
    public static ChargerSnapshotMetric Collect(ChargerBase charger, ushort stationId, Time simTime, float utilization) =>
        new()
        {
            SimTime = simTime,
            StationId = stationId,
            ChargerId = charger.Id,
            MaxKWh = charger.MaxPowerKW,
            Utilization = utilization,
        };
}
