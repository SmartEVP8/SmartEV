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
    /// Gets station this snapshot was taken from.
    /// </summary>
    required public ushort StationId { get; init; }

    /// <summary>
    /// Gets the average charger utilisation across all chargers at the station (0.0–1.0).
    /// Utilisation per charger = actual power delivered / max power capacity.
    /// </summary>
    required public float Utilisation { get; init; }

    /// <summary> Gets the average number of EVs queued per charger at snapshot time.</summary>
    required public float AvgQueueSize { get; init; }

    /// <summary> Gets the station's energy price in DKK/kWh at snapshot time.</summary>
    required public float AvgPrice { get; init; }

    /// <summary> Gets percentage of chargers with at least one active connector (0–100).</summary>
    required public float ActiveChargersPct { get; init; }

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
                Utilisation = 0f,
                AvgQueueSize = 0f,
                AvgPrice = 0f,
                ActiveChargersPct = 0f,
            };
        }

        var totalUtilisation = 0f;
        var totalQueued = 0;
        var activeCount = 0;

        foreach (var charger in chargers)
        {
            var delivered = getDeliveredKW(charger);
            totalUtilisation += charger.MaxPowerKW > 0
                ? (float)(delivered / charger.MaxPowerKW)
                : 0f;

            var queue = charger switch
            {
                SingleCharger singleCharger => singleCharger.Queue,
                DualCharger dualCharger => dualCharger.Queue,
                _ => null
            };

            if (queue is not null)
            {
                totalQueued += queue.Count;
                if (queue.Count > 0) activeCount++;
            }
        }

        return new SnapshotMetric
        {
            SimTime = simTime,
            StationId = station.Id,
            Utilisation = totalUtilisation / chargers.Count,
            AvgQueueSize = (float)totalQueued / chargers.Count,
            AvgPrice = station.CalculatePrice(day, hour),
            ActiveChargersPct = (float)activeCount / chargers.Count * 100f,
        };
    }
}