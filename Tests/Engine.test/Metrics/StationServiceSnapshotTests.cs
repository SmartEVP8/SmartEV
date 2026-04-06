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
        var service = TestData.StationService(stations, scheduler, evStore);

        evStore.TryAllocate(
        (_, ref e) =>
        {
            e = TestData.EV();
            e.Battery.StateOfCharge = 0.1f;
        }, out var evId);

        service.HandleArrivalAtStation(new ArriveAtStation(evId, 1, 1.0, new Time(0)));
        var endChargingEvent = scheduler.GetNextEvent() as EndCharging;

        Assert.NotNull(endChargingEvent);

        service.HandleEndCharging(endChargingEvent);
        var snapshotTime = endChargingEvent.Time + 60;
        var (chargerSnapshots, _) = service.CollectAllSnapshots(snapshotTime);
        var snapshots = chargerSnapshots.ToList();

        Assert.Single(snapshots);
        Assert.Equal(1, snapshots[0].ChargerId);
    }

    [Fact]
    public void CollectAllSnapshots_CapturesAndResetsStationCounters()
    {
        var scheduler = new EventScheduler();
        var evStore = new EVStore(3);
        var stations = TestData.Stations((1, 1.0, 1.0));
        var service = TestData.StationService(stations, scheduler, evStore);
        var ev1 = TestData.EV();
        evStore.Set(0, ref ev1);
        var ev2 = TestData.EV();
        evStore.Set(1, ref ev2);
        var ev3 = TestData.EV();
        evStore.Set(2, ref ev3);

        service.HandleReservationRequest(new ReservationRequest(EVId: 0, StationId: 1, Time: new Time(0), DurationToStation: 10));
        service.HandleReservationRequest(new ReservationRequest(EVId: 1, StationId: 1, Time: new Time(0), DurationToStation: 10));
        service.HandleReservationRequest(new ReservationRequest(EVId: 2, StationId: 1, Time: new Time(0), DurationToStation: 10));

        service.HandleCancelRequest(new CancelRequest(EVId: 2, StationId: 1, Time: new Time(10)));

        var simTime = new Time(3600);

        // First collection should capture 3 reservations and 1 cancellation
        var (_, stationMetrics) = service.CollectAllSnapshots(simTime);
        var metric = stationMetrics.First(m => m.StationId == 1);

        Assert.Equal(3u, metric.Reservations);
        Assert.Equal(1u, metric.Cancellations);

        // Second collection should reflect that the window counters reset to 0
        var (_, nextStationMetrics) = service.CollectAllSnapshots(simTime + 3600);
        var nextMetric = nextStationMetrics.First(m => m.StationId == 1);

        Assert.Equal(0u, nextMetric.Reservations);
        Assert.Equal(0u, nextMetric.Cancellations);
    }
}