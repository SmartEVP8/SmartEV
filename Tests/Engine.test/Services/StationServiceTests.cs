namespace Engine.test.Services;

using Core.Shared;
using Core.Charging;
using Core.Charging.ChargingModel;
using Engine.Events;
using Engine.Routing;
using Engine.Services;
using Engine.test.Builders;
using Engine.Vehicles;

public class StationServiceTests
{
    [Fact]
    public void TwoCars_DualCharger_BothReceiveCharge()
    {
        var (service, scheduler, evStore) = BuildDual();

        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index1);
        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index2);

        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index1, StationId: 1, TargetSoC: 0.8, Time: 0));
        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index2, StationId: 1, TargetSoC: 0.8, Time: 0));

        var end1 = AsEndCharging(scheduler.GetNextEvent());
        var end2 = AsEndCharging(scheduler.GetNextEvent());

        Assert.NotEqual(end1.EVId, end2.EVId);
        Assert.Equal(end1.ChargerId, end2.ChargerId);
        Assert.True(end1.Time > 0);
        Assert.True(end2.Time > 0);
    }

    [Fact]
    public void ThreeEVs_SingleCharger_FirstStartsRemainingQueues()
    {
        var (service, scheduler, evStore) = BuildSingle();

        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index1);
        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index2);
        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index3);

        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index1, StationId: 1, TargetSoC: 0.6, Time: 0));
        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index2, StationId: 1, TargetSoC: 0.8, Time: 0));
        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index3, StationId: 1, TargetSoC: 0.8, Time: 0));

        var firstEnd = AsEndCharging(scheduler.GetNextEvent());
        Assert.Equal(index1, firstEnd.EVId);
        Assert.Equal(2, service.GetChargerState(chargerId: 1)!.Queue.Count);
        Assert.Null(scheduler.GetNextEvent());

        service.HandleEndCharging(firstEnd);
        Assert.Single(service.GetChargerState(chargerId: 1)!.Queue);

        var secondEnd = AsEndCharging(scheduler.GetNextEvent());
        Assert.Equal(index2, secondEnd.EVId);
    }

    [Fact]
    public void ThreeEVs_DualCharger_TwoChargeTogetherThirdQueues()
    {
        var (service, scheduler, evStore) = BuildDual(maxPowerKW: 200);

        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index1);
        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index2);
        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index3);

        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index1, StationId: 1, TargetSoC: 0.8, Time: 0));
        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index2, StationId: 1, TargetSoC: 0.8, Time: 0));
        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index3, StationId: 1, TargetSoC: 0.8, Time: 0));

        var ev1End = AsEndCharging(scheduler.GetNextEvent());
        Assert.Equal(index1, ev1End.EVId);
        Assert.Single(service.GetChargerState(chargerId: 1)!.Queue);

        service.HandleEndCharging(ev1End);

        var nextA = AsEndCharging(scheduler.GetNextEvent());
        var nextB = AsEndCharging(scheduler.GetNextEvent());
        Assert.Empty(service.GetChargerState(chargerId: 1)!.Queue);

        var ev2Event = nextA.EVId == index2 ? nextA : nextB;
        var ev3Event = nextA.EVId == index3 ? nextA : nextB;

        Assert.Equal(index2, ev2Event.EVId);
        Assert.Equal(index3, ev3Event.EVId);
        Assert.True(ev2Event.Time > ev1End.Time);
    }

    [Fact]
    public void HandleReservationRequest_SetsReservation()
    {
        var scheduler = new EventScheduler([]);
        var evStore = new EVStore(1);
        var router = TestData.OSRMRouter;

        var stations = TestData.Stations((1, 5.0, 5.0));
        var stationService = new StationService(
            stations: [.. stations.Values],
            integrator: null!,
            scheduler: scheduler,
            evStore: evStore,
            applyNewPath: new ApplyNewPath(router));

        var ev = TestData.EV();
        evStore.Set(0, ref ev);

        stationService.HandleReservationRequest(
            new ReservationRequest(0, 1, new Time(0), 0));

        Assert.Equal(1, (ushort)evStore.Get(0).HasReservationAtStationId!);
    }

    [Fact]
    public void HandleReservationRequest_CancelsPreviousReservation()
    {
        var scheduler = new EventScheduler([]);
        var evStore = new EVStore(1);
        var router = TestData.OSRMRouter;

        var stations = TestData.Stations(
            (1, 1.0, 1.0),
            (2, 2.0, 2.0));

        var stationService = new StationService(
            stations: [.. stations.Values],
            integrator: null!,
            scheduler: scheduler,
            evStore: evStore,
            applyNewPath: new ApplyNewPath(router));

        var ev = TestData.EV();
        ev.HasReservationAtStationId = 1;

        evStore.Set(0, ref ev);

        var oldStation = stations[1];
        var newStation = stations[2];

        oldStation.IncrementReservations();

        Assert.Equal(0, oldStation.TotalCancellations);
        Assert.Equal(0, newStation.TotalReservations);

        stationService.HandleReservationRequest(
            new ReservationRequest(0, 2, new Time(0), 10));

        Assert.Equal(2, (ushort)evStore.Get(0).HasReservationAtStationId!);

        Assert.Equal(1, oldStation.TotalCancellations);
        Assert.Equal(1, newStation.TotalReservations);
    }

    [Fact]
    public async Task CheckReservationOrderIsCorrect()
    {
        var totalRequest = 100;
        var scheduler = new EventScheduler([]);
        var evStore = new EVStore(totalRequest);
        var router = TestData.OSRMRouter;

        var staionId = (ushort)1;
        var stations = TestData.Stations(
            (staionId, 1.0, 1.0));

        var stationService = new StationService(
            stations: [.. stations.Values],
            integrator: null!,
            scheduler: scheduler,
            evStore: evStore,
            applyNewPath: new ApplyNewPath(router));

        evStore.TryAllocate(totalRequest, (index, ref ev) =>
        {
            ev = TestData.EV();
        });

        for (var i = 0; i < totalRequest; i++)
        {
            var reservation = new ReservationRequest(i, staionId, 0, 1);
            stationService.HandleReservationRequest(reservation);
        }

        await stationService.WaitForAllScheduled();

        for (var i = 0; i < totalRequest; i++)
        {
            var ev = scheduler.GetNextEvent();
            Assert.NotNull(ev);
            Assert.IsType<ArriveAtStation>(ev);
            Assert.Equal(i, ((ArriveAtStation)ev).EVId);
        }
    }

    [Fact]
    public void HandleReservationRequest_SetsReservation()
    {
        var scheduler = new EventScheduler([]);
        var evStore = new EVStore(1);
        var stations = TestData.Stations((1, 5.0, 5.0));
        var service = TestData.StationService(stations, scheduler, evStore);

        var ev = TestData.EV();
        evStore.Set(0, ref ev);

        service.HandleReservationRequest(new ReservationRequest(0, 1, new Time(0), 0));

        Assert.Equal(1, (ushort)evStore.Get(0).HasReservationAtStationId!);
    }

    [Fact]
    public void HandleReservationRequest_CancelsPreviousReservation()
    {
        var scheduler = new EventScheduler([]);
        var evStore = new EVStore(1);
        var stations = TestData.Stations((1, 1.0, 1.0), (2, 2.0, 2.0));
        var service = TestData.StationService(stations, scheduler, evStore);

        var ev = TestData.EV();
        ev.HasReservationAtStationId = 1;
        evStore.Set(0, ref ev);

        var oldStation = stations[1];
        var newStation = stations[2];
        oldStation.IncrementReservations();

        Assert.Equal(0, oldStation.TotalCancellations);
        Assert.Equal(0, newStation.TotalReservations);

        service.HandleReservationRequest(new ReservationRequest(0, 2, new Time(0), 10));

        Assert.Equal(2, (ushort)evStore.Get(0).HasReservationAtStationId!);
        Assert.Equal(1, oldStation.TotalCancellations);
        Assert.Equal(1, newStation.TotalReservations);
    }

    [Fact]
    public async Task CheckReservationOrderIsCorrect()
    {
        var totalRequest = 100;
        var scheduler = new EventScheduler([]);
        var evStore = new EVStore(totalRequest);
        var stationId = (ushort)1;
        var stations = TestData.Stations((stationId, 1.0, 1.0));
        var service = TestData.StationService(stations, scheduler, evStore);

        evStore.TryAllocate(totalRequest, (index, ref ev) => { ev = TestData.EV(); });

        for (var i = 0; i < totalRequest; i++)
            service.HandleReservationRequest(new ReservationRequest(i, stationId, 0, 1));

        await service.WaitForAllScheduled();

        for (var i = 0; i < totalRequest; i++)
        {
            var ev = scheduler.GetNextEvent();
            Assert.NotNull(ev);
            Assert.IsType<ArriveAtStation>(ev);
            Assert.Equal(i, ((ArriveAtStation)ev).EVId);
        }
    }

    private static (StationService service, EventScheduler scheduler, EVStore evStore) BuildSingle(int maxPowerKW = 150)
    {
        var charger = TestData.SingleCharger(1, maxPowerKW: maxPowerKW);
        var station = TestData.Station(1, chargers: [charger]);
        var stations = new Dictionary<ushort, Station> { [1] = station };
        var scheduler = new EventScheduler([]);
        var evStore = new EVStore(10);
        var applyNewPath = new ApplyNewPath(TestData.OSRMRouter);
        var metrics = TestData.MetricsService();
        var service = TestData.StationService(stations, scheduler, evStore, applyNewPath);
        return (service, scheduler, evStore);
    }

    private static (StationService service, EventScheduler scheduler, EVStore evStore) BuildDual(int maxPowerKW = 150)
    {
        var charger = TestData.DualCharger(1, maxPowerKW: maxPowerKW);
        var station = TestData.Station(1, chargers: [charger]);
        var stations = new Dictionary<ushort, Station> { [1] = station };
        var scheduler = new EventScheduler([]);
        var evStore = new EVStore(10);
        var metrics = TestData.MetricsService();
        var service = TestData.StationService(stations, scheduler, evStore);
        return (service, scheduler, evStore);
    }

    private static EndCharging AsEndCharging(Event? e)
    {
        Assert.NotNull(e);
        Assert.IsType<EndCharging>(e);
        return (EndCharging)e!;
    }
}