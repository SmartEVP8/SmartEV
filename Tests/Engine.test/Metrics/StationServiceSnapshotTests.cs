namespace Engine.test.Metrics;

using Core.Charging;
using Core.Shared;
using Engine.Events;
using Engine.test.Builders;
using Engine.Vehicles;

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
        var settings = TestData.DefaultSettings();
        var service = TestData.StationService(stations, scheduler, evStore, settings);

        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var evId);

        service.HandleArrivalAtStation(new ArriveAtStation(evId, 1, 0.8, new Time(0)));

        var endChargingEvent = scheduler.GetNextEvent();
        Assert.NotNull(endChargingEvent);
        Assert.IsType<EndCharging>(endChargingEvent);
        service.HandleEndCharging((EndCharging)endChargingEvent);

        var (chargerSnapshots, _) = service.CollectAllSnapshots(new Time(600));
        var snapshots = chargerSnapshots.ToList();

        Assert.Single(snapshots);
        Assert.Equal(1, snapshots[0].ChargerId);
        Assert.True(snapshots[0].Utilization > 0f);
    }

    [Fact]
    public void CollectChargerSnapshots_IncludesUtilizationFromIntegrator()
    {
        var scheduler = new EventScheduler();
        var evStore = new EVStore(5);
        var charger = TestData.SingleCharger(1, maxPowerKW: 120);
        var station = TestData.Station(1, chargers: [charger]);
        var stations = new Dictionary<ushort, Station> { [1] = station };
        var settings = TestData.DefaultSettings();
        var service = TestData.StationService(stations, scheduler, evStore, settings);

        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var evId);

        service.HandleArrivalAtStation(new ArriveAtStation(evId, 1, 0.8, new Time(0)));

        var (chargerSnapshots, _) = service.CollectAllSnapshots(new Time(60));
        var snapshots = chargerSnapshots.ToList();

        Assert.Single(snapshots);
        Assert.Equal(1, snapshots[0].ChargerId);
        Assert.InRange(snapshots[0].Utilization, 0.1f, 1f);
    }
}
