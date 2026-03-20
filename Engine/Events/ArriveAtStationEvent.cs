namespace Engine.Services;

using Core.Charging.ChargingModel;
using Engine.Events;
using Engine.Charging;
using Engine.Metrics;

public class ArriveAtStationEventHandler(
    Dictionary<ushort, List<ChargerState>> stationChargers,
    Action<ChargerState, int> startCharging,
    StationSnapshotMetric metrics)
{
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