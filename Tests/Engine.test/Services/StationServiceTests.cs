namespace Engine.test.Services;

using Core.Charging.ChargingModel;
using Core.Shared;
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
        // Two cars arrive at a dual charger simultaneously.
        // Both should start charging and have EndCharging events scheduled.
        var (service, scheduler, evStore) = BuildDual();

        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index1);
        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index2);

        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index1, StationId: 1, TargetSoC: 0.8, Time: 0));
        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index2, StationId: 1, TargetSoC: 0.8, Time: 0));

        var end1 = AsEndCharging(scheduler.GetNextEvent());
        var end2 = AsEndCharging(scheduler.GetNextEvent());

        // Both cars are charging different EVIds, same charger
        Assert.NotEqual(end1.EVId, end2.EVId);
        Assert.Equal(end1.ChargerId, end2.ChargerId);

        // Finish times should be in the future
        Assert.True(end1.Time > 0);
        Assert.True(end2.Time > 0);
    }

    [Fact]
    public void ThreeEVs_SingleCharger_FirstStartsRemainingQueues()
    {
        // Single charger: first EV starts immediately, second and third queue.
        // After first finishes, second should start.
        var (service, scheduler, evStore) = BuildSingle();

        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index1);
        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index2);
        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index3);

        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index1, StationId: 1, TargetSoC: 0.6, Time: 0));
        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index2, StationId: 1, TargetSoC: 0.8, Time: 0));
        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index3, StationId: 1, TargetSoC: 0.8, Time: 0));

        // Only ev1 should have an EndCharging scheduled — ev2 and ev3 are queued
        var firstEnd = AsEndCharging(scheduler.GetNextEvent());
        var expectedQueueSize = 2;
        Assert.Equal(index1, firstEnd.EVId);
        Assert.Equal(expectedQueueSize, service.GetChargerState(chargerId: 1)!.Queue.Count);
        Assert.Null(scheduler.GetNextEvent()); // ev2 and ev3 still queued

        // ev1 finishes — service should start ev2
        service.HandleEndCharging(firstEnd);
        Assert.Single(service.GetChargerState(chargerId: 1)!.Queue); // ev3 still queued

        var secondEnd = AsEndCharging(scheduler.GetNextEvent());
        Assert.Equal(index2, secondEnd.EVId);
    }

    [Fact]
    public void ThreeEVs_DualCharger_TwoChargeTogetherThirdQueues()
    {
        // Dual charger — first two EVs fill both sides, third queues.
        // After one finishes, third should start and power is redistributed.
        var (service, scheduler, evStore) = BuildDual(maxPowerKW: 200);

        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index1);
        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index2);
        evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index3);

        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index1, StationId: 1, TargetSoC: 0.8, Time: 0));
        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index2, StationId: 1, TargetSoC: 0.8, Time: 0));
        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index3, StationId: 1, TargetSoC: 0.8, Time: 0));

        // Both sides occupied — ev3 is queued
        var ev1End = AsEndCharging(scheduler.GetNextEvent());
        Assert.Equal(index1, ev1End.EVId);
        Assert.Single(service.GetChargerState(chargerId: 1)!.Queue);

        service.HandleEndCharging(ev1End);

        // ev2 rescheduled + ev3 newly scheduled
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
        var scheduler = new EventScheduler();
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
        var scheduler = new EventScheduler();
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

    private static (StationService service, EventScheduler scheduler, EVStore evStore) BuildSingle(
        int maxPowerKW = 150)
    {
        var charger = TestData.SingleCharger(1, maxPowerKW: maxPowerKW);
        var station = TestData.Station(1, chargers: [charger]);
        var scheduler = new EventScheduler();
        var integrator = new ChargingIntegrator(stepSeconds: 60);
        var evStore = new EVStore(10);
        var applyNewPath = new ApplyNewPath(TestData.OSRMRouter);
        var service = new StationService([station], integrator, scheduler, evStore, applyNewPath);

        return (service, scheduler, evStore);
    }

    private static (StationService service, EventScheduler scheduler, EVStore evStore) BuildDual(
        int maxPowerKW = 150)
    {
        var charger = TestData.DualCharger(1, maxPowerKW: maxPowerKW);
        var station = TestData.Station(1, chargers: [charger]);
        var scheduler = new EventScheduler();
        var integrator = new ChargingIntegrator(stepSeconds: 60);
        var evStore = new EVStore(10);
        var applyNewPath = new ApplyNewPath(TestData.OSRMRouter);
        var service = new StationService([station], integrator, scheduler, evStore, applyNewPath);

        return (service, scheduler, evStore);
    }

    private static EndCharging AsEndCharging(Event? e)
    {
        Assert.NotNull(e);
        Assert.IsType<EndCharging>(e);
        return (EndCharging)e;
    }
}
