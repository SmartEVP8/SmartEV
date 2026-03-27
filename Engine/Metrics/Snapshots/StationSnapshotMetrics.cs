namespace Engine.Metrics.Snapshots;

using Core.Charging;
using System;
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
    required public Time SimTime { get; init; }

    /// <summary>
    /// Gets the station this snapshot was taken from.
    /// </summary>
    required public ushort StationId { get; init; }

    /// <summary>
    /// Gets the total power delivered across all chargers in kW.
    /// </summary>
    required public float TotalDeliveredKW { get; init; }

    /// <summary>
    /// Gets the total maximum power capacity across all chargers in kW.
    /// </summary>
    required public float TotalMaxKW { get; init; }

    /// <summary>
    /// Gets the total number of EVs queued across all chargers at snapshot time.
    /// </summary>
    required public int TotalQueueSize { get; init; }

    /// <summary>
    /// Gets the station's energy price in DKK/kWh at snapshot time.
    /// </summary>
    required public float Price { get; init; }

    /// <summary>
    /// Gets the number of chargers with at least one EV queued.
    /// </summary>
    required public int ActiveChargers { get; init; }

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
    /// Collects a snapshot from a station at the given simulation time.
    /// </summary>
    /// <param name="station">The station to snapshot.</param>
    /// <param name="simTime">Current simulation time in seconds.</param>
    /// <param name="day">Current day of week (for price lookup).</param>
    /// <param name="hour">Current hour 0–23 (for price lookup).</param>
    /// 
    /// A delegate that returns the actual power currently being delivered (in kW)
    /// for a given charger. Provided by the caller since power state lives outside
    /// this record. // TODO: Should be implemented somehow later.
    /// </param>
    /// <returns>A <see cref="SnapshotMetric"/> containing the collected metrics for the station.</returns>
    public static StationSnapshotMetric Collect(
        Station station,
        Time simTime,
        DayOfWeek day,
        int hour)
    {
        var totalDeliveredKW = 0f;
        var totalMaxKW = 0f;
        var totalQueueSize = 0;
        var activeChargers = 0;

        foreach (var charger in station.Chargers)
        {
            totalDeliveredKW += totalDeliveredKW; // TODO: ADD METRICS BACK HERE
            totalMaxKW += charger.MaxPowerKW;
            totalQueueSize += charger.Queue.Count;
            if (charger.Queue.Count > 0) activeChargers++;
        }

        var (reservations, cancellations) = station.CountReservationsCancellations();

        return new StationSnapshotMetric
        {
            SimTime = simTime,
            StationId = station.Id,
            TotalDeliveredKW = totalDeliveredKW,
            TotalMaxKW = totalMaxKW,
            TotalQueueSize = totalQueueSize,
            Price = station.UpdatePrice(day, hour),
            ActiveChargers = activeChargers,
            TotalChargers = station.Chargers.Count,
            Reservations = reservations,
            Cancellations = cancellations,
        };
    }
}