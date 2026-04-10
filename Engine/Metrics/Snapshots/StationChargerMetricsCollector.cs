namespace Engine.Metrics.Snapshots;

using Core.Charging;
using Core.Shared;
using Engine.Services.StationServiceHelpers;

/// <summary>
/// Dedicated service for generating point-in-time metrics snapshots from current station states.
/// </summary>
public class StationMetricsCollector(Time snapshotInterval)
{
    /// <summary>
    /// Collects charger and station snapshot metrics for the given simulation time and station states.
    /// </summary>
    /// <param name="simNow">The current simulation time.</param>
    /// <param name="stationChargers">A dictionary mapping station IDs to their list of charger states.</param>
    /// <param name="stationIndex">A dictionary mapping station IDs to station details.</param>
    /// <param name="windowReservations">A dictionary tracking the number of reservations made at each station during the current snapshot window.</param>
    /// <param name="windowCancellations">A dictionary tracking the number of cancellations made at each station during the current snapshot window.</param>
    /// <returns>A tuple containing the list of charger snapshot metrics and station snapshot metrics.</returns>
    public (IEnumerable<ChargerSnapshotMetric> Chargers, IEnumerable<StationSnapshotMetric> Stations) Collect(
        Time simNow,
        IReadOnlyDictionary<ushort, List<ChargerState>> stationChargers,
        IReadOnlyDictionary<ushort, Station> stationIndex,
        IDictionary<ushort, uint> windowReservations,
        IDictionary<ushort, uint> windowCancellations)
    {
        var chargerMetrics = new List<ChargerSnapshotMetric>();
        var stationMetrics = new List<StationSnapshotMetric>();

        foreach (var (stationId, chargerStates) in stationChargers)
        {
            var station = stationIndex[stationId];
            var totalDeliveredKWh = 0f;
            var totalMaxKWh = 0f;
            var totalQueueSize = 0;

            foreach (var state in chargerStates)
            {
                state.AccumulateEnergy(simNow);
                var deliveredKWhInWindow = (float)state.Window.DeliveredKWh;

                var targetEVDemandKWh = 0f;
                if (state.SessionA is not null)
                    targetEVDemandKWh += (float)Math.Max(0, (state.SessionA.EV.TargetSoC - state.SessionA.GetCurrentSoC(simNow)) * state.SessionA.EV.CapacityKWh);

                if (state.SessionB is not null)
                    targetEVDemandKWh += (float)Math.Max(0, (state.SessionB.EV.TargetSoC - state.SessionB.GetCurrentSoC(simNow)) * state.SessionB.EV.CapacityKWh);

                var snapshotDurationHours = snapshotInterval / 3600f;
                var maxPossibleKWh = state.Charger.MaxPowerKW * snapshotDurationHours;
                var utilizationInWindow = maxPossibleKWh > 0
                    ? Math.Clamp(deliveredKWhInWindow / maxPossibleKWh, 0f, 1f)
                    : 0f;

                var queueSizeInWindow = state.Queue.Count;

                chargerMetrics.Add(ChargerSnapshotMetric.Collect(
                    state.Charger,
                    stationId,
                    simNow,
                    queueSizeInWindow,
                    utilizationInWindow,
                    deliveredKWhInWindow,
                    targetEVDemandKWh));

                totalDeliveredKWh += deliveredKWhInWindow;
                totalMaxKWh += state.Charger.MaxPowerKW * snapshotDurationHours;
                totalQueueSize += queueSizeInWindow;

                // Reset the struct for the next window
                var window = state.Window;
                window.Reset(state.Queue.Count);
                state.Window = window;
            }

            windowReservations.TryGetValue(stationId, out var reservations);
            windowCancellations.TryGetValue(stationId, out var cancellations);

            windowReservations[stationId] = 0;
            windowCancellations[stationId] = 0;

            stationMetrics.Add(new StationSnapshotMetric
            {
                SimTime = (uint)simNow,
                StationId = stationId,
                TotalDeliveredKWh = totalDeliveredKWh,
                TotalMaxKWh = totalMaxKWh,
                TotalQueueSize = totalQueueSize,
                Price = station.GetPrice(simNow),
                TotalChargers = chargerStates.Count,
                Reservations = reservations,
                Cancellations = cancellations,
            });
        }

        return (chargerMetrics, stationMetrics);
    }
}
