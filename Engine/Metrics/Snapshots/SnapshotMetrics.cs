namespace Engine.Metrics;

using Core.Charging;
using System;

/// <summary>
/// A point-in-time snapshot of a single station's metrics.
/// Collected once per simulation hour.
/// </summary>
public record SnapshotMetric
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
    /// Collects a snapshot from a station at the given simulation time.
    /// </summary>
    /// <param name="station">The station to snapshot.</param>
    /// <param name="simTime">Current simulation time in seconds.</param>
    /// <param name="day">Current day of week (for price lookup).</param>
    /// <param name="hour">Current hour 0–23 (for price lookup).</param>
    /// <param name="getDeliveredKW">
    /// A delegate that returns the actual power currently being delivered (in kW)
    /// for a given charger. Provided by the caller since power state lives outside
    /// this record.
    /// </param>
    /// <returns>A <see cref="SnapshotMetric"/> containing the collected metrics for the station.</returns>
    public static SnapshotMetric Collect(
        Station station,
        uint simTime,
        DayOfWeek day,
        int hour,
        Func<ChargerBase, double> getDeliveredKW)
    {
        var chargers = station.Chargers;

        if (chargers.Count == 0)
        {
            return new SnapshotMetric
            {
                SimTime = simTime,
                StationId = station.Id,
                TotalDeliveredKW = 0f,
                TotalMaxKW = 0f,
                TotalQueueSize = 0,
                Price = 0f,
                ActiveChargers = 0,
                TotalChargers = 0,
            };
        }

        var totalDeliveredKW = 0f;
        var totalMaxKW = 0f;
        var totalQueueSize = 0;
        var activeChargers = 0;

        foreach (var charger in chargers)
        {
            totalDeliveredKW += (float)getDeliveredKW(charger);
            totalMaxKW += charger.MaxPowerKW;

            var queue = charger switch
            {
                SingleCharger singleCharger => singleCharger.Queue,
                DualCharger dualCharger => dualCharger.Queue,
                _ => null
            };

            if (queue is not null)
            {
                totalQueueSize += queue.Count;
                if (queue.Count > 0) activeChargers++;
            }
        }

        return new SnapshotMetric
        {
            SimTime = simTime,
            StationId = station.Id,
            TotalDeliveredKW = totalDeliveredKW,
            TotalMaxKW = totalMaxKW,
            TotalQueueSize = totalQueueSize,
            Price = station.CalculatePrice(day, hour),
            ActiveChargers = activeChargers,
            TotalChargers = chargers.Count,
        };
    }
}