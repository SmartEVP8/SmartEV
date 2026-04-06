namespace Engine.test.Metrics;

using Core.Charging;
using Core.Shared;
using Engine.Events;
using Engine.test.Builders;
using Engine.Vehicles;
using Xunit;

public class StationServiceSnapshotTests
{
    [Fact]
    public void CollectChargerSnapshots_CapturesWindowActivity_EvenWhenIdleAtTick()
    {
        var scheduler = new EventScheduler();
        var evStore = new EVStore(5);
        var charger = TestData.SingleCharger(1, maxPowerKW: 120);
        var station = TestData.Station(1, chargers: [charger]);
        var stations = new Dictionary<ushort, Station> { [1] = station };
        var service = TestData.StationService(stations, scheduler, evStore);

        evStore.TryAllocate(
            (_, ref e) =>
        {
            e = TestData.EV();
            e.Battery.StateOfCharge = 0.1f;
        }, out var evId);

        // FIX: Set Target SoC to 1.0 (100%)
        service.HandleArrivalAtStation(new ArriveAtStation(evId, 1, 1.0, new Time(0)));

        var endChargingEvent = scheduler.GetNextEvent() as EndCharging;
        Assert.NotNull(endChargingEvent);

        // Complete the charging session
        service.HandleEndCharging(endChargingEvent);

        // Take a snapshot AFTER the session finished.
        var snapshotTime = endChargingEvent.Time + 60;
        var (chargerSnapshots, _) = service.CollectAllSnapshots(snapshotTime);
        var snapshots = chargerSnapshots.ToList();

        Assert.Single(snapshots);
        Assert.Equal(1, snapshots[0].ChargerId);

        // Utilization should now be > 0 because a full charge cycle actually occurred
        Assert.Equal(1f, snapshots[0].Utilization);
    }
}