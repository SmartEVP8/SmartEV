namespace Engine.Events;

using Core.Charging.ChargingModel;
using Engine.Charging;
using Engine.Metrics.Snapshots;

/// <summary>
/// Handles the arrival of a connected car at a charging station.
/// </summary>
public class ArriveAtStationEventHandler(
    Dictionary<ushort, List<ChargerState>> stationChargers,
    Action<ChargerState, int> startCharging,
    StationSnapshotMetric metrics)
{
    /// <summary>
    /// Handles the arrival of a connected car at a charging station.
    /// </summary>
    /// <param name="e">The arrival event containing station information.</param>
    /// <param name="car">The connected car arriving at the station.</param>
    public void Handle(ArriveAtStation e, ConnectedCar car)
    {
        if (!stationChargers.TryGetValue(e.StationId, out var chargers))
            return;

        var compatible = chargers
            .Where(cs => cs.Charger.GetSockets().Contains(car.Socket))
            .ToList();

        if (compatible.Count == 0)
            return;

        var target = compatible.FirstOrDefault(cs => cs.IsFree)
            ?? compatible.MinBy(cs => cs.Queue.Count)!;


        metrics.ArrivalTimes[e.EVId] = e.Time;

        if (target.IsFree)
        {
            startCharging(target, e.Time);
        }
        else
        {
            target.Queue.Enqueue((e.EVId, car));
            metrics.TotalQueueSize++;
        }
    }
}