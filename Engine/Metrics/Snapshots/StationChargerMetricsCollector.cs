namespace Engine.Metrics.Snapshots;

using Core.Charging;
using Core.Shared;
using Engine.Utils;

/// <summary>
/// Dedicated service for generating point-in-time metrics snapshots from current station states.
/// </summary>
public class StationMetricsCollector(Time snapshotInterval)
{
    /// <summary>
    /// Collects charger and station snapshot metrics for the given simulation time and station states.
    /// </summary>
    /// <param name="simNow">The current simulation time.</param>
    /// <param name="stationIndex">A dictionary mapping station IDs to station details.</param>
    /// <param name="windowReservations">A dictionary tracking the number of reservations made at each station during the current snapshot window.</param>
    /// <param name="windowCancellations">A dictionary tracking the number of cancellations made at each station during the current snapshot window.</param>
    /// <returns>A tuple containing the list of charger snapshot metrics and station snapshot metrics.</returns>
    public (IEnumerable<ChargerSnapshotMetric> Chargers, IEnumerable<StationSnapshotMetric> Stations) Collect(
    Time simNow,
    IReadOnlyDictionary<ushort, Station> stationIndex,
    IDictionary<ushort, uint> windowReservations,
    IDictionary<ushort, uint> windowCancellations)
    {
        var chargerMetrics = new List<ChargerSnapshotMetric>();
        var stationMetrics = new List<StationSnapshotMetric>();

        foreach (var (stationId, station) in stationIndex)
        {
            var totalDeliveredKWh = 0f;
            var totalMaxKWh = 0f;
            var totalQueueSize = 0;

            foreach (var charger in station.Chargers)
            {
                charger.AccumulateEnergy(simNow);
                var deliveredKWhInWindow = (float)charger.Window.DeliveredKWh;
                var targetEVDemandKWh = CalculateTargetEVDemandKWh(charger, simNow);
                var snapshotDurationHours = (float)snapshotInterval / Time.MillisecondsPerHour;
                var maxPossibleKWh = charger.MaxPowerKW * snapshotDurationHours;
                var utilizationInWindow = maxPossibleKWh > 0
                    ? Math.Clamp(deliveredKWhInWindow / maxPossibleKWh, 0f, 1f)
                    : 0f;

                var queueSizeInWindow = charger.Queue.Count;

                chargerMetrics.Add(ChargerSnapshotMetric.Collect(
                    charger,
                    stationId,
                    simNow,
                    queueSizeInWindow,
                    utilizationInWindow,
                    deliveredKWhInWindow,
                    targetEVDemandKWh));

                totalDeliveredKWh += deliveredKWhInWindow;
                totalMaxKWh += charger.MaxPowerKW * snapshotDurationHours;
                totalQueueSize += queueSizeInWindow;

                var window = charger.Window;
                window.Reset(charger.Queue.Count);
                charger.Window = window;
            }

            windowReservations.TryGetValue(stationId, out var reservations);
            windowCancellations.TryGetValue(stationId, out var cancellations);

            windowReservations[stationId] = 0;
            windowCancellations[stationId] = 0;

            stationMetrics.Add(new StationSnapshotMetric
            {
                SimTime = simNow,
                StationId = stationId,
                TotalDeliveredKWh = totalDeliveredKWh,
                TotalMaxKWh = totalMaxKWh,
                TotalQueueSize = totalQueueSize,
                Price = station.GetPrice(simNow),
                TotalChargers = station.Chargers.Count,
                Reservations = reservations,
                Cancellations = cancellations,
            });
        }

        return (chargerMetrics, stationMetrics);
    }

    private float CalculateTargetEVDemandKWh(ChargerBase charger, Time simNow)
    {
        var targetEVDemandKWh = 0f;
        switch (charger)
        {
            case SingleCharger c:
                if (c.Session is not null)
                    targetEVDemandKWh += (float)Math.Max(0, (c.Session.EV.TargetSoC - c.Session.GetCurrentSoC(simNow)) * c.Session.EV.CapacityKWh);
                return targetEVDemandKWh;

            case DualCharger c:
                if (c.SessionA is not null)
                    targetEVDemandKWh += (float)Math.Max(0, (c.SessionA.EV.TargetSoC - c.SessionA.GetCurrentSoC(simNow)) * c.SessionA.EV.CapacityKWh);

                if (c.SessionB is not null)
                    targetEVDemandKWh += (float)Math.Max(0, (c.SessionB.EV.TargetSoC - c.SessionB.GetCurrentSoC(simNow)) * c.SessionB.EV.CapacityKWh);
                return targetEVDemandKWh;
            default:
                throw new SkillissueException("Do we have a third type of charger? :O");
        }
    }
}
