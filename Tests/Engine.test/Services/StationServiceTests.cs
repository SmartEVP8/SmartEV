namespace Engine.test.Services;

using Core.Charging;
using Engine.Events;
using Engine.Services;
using Engine.test.Builders;
using Core.test.Builders;
using Engine.Vehicles;
using Core.Vehicles;

public class StationServiceTests
{
    private ushort stationId = 1;

    [Fact]
    public void TwoCars_DualCharger_BothReceiveCharge()
    {
        // Two cars arrive at a dual charger simultaneously.
        // Both should start charging and have EndCharging events scheduled.
        var (service, scheduler, evStore, _) = BuildDual();

        evStore.TryAllocate((_, ref e) => { e = CoreTestData.EV(); }, out var index1);
        evStore.TryAllocate((_, ref e) => { e = CoreTestData.EV(); }, out var index2);

        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index1, StationId: stationId, TargetSoC: 0.8, Time: 0));
        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index2, StationId: stationId, TargetSoC: 0.8, Time: 0));

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
        var (service, scheduler, evStore, charger) = BuildSingle();

        evStore.TryAllocate((_, ref e) => { e = CoreTestData.EV(); }, out var index1);
        evStore.TryAllocate((_, ref e) => { e = CoreTestData.EV(); }, out var index2);
        evStore.TryAllocate((_, ref e) => { e = CoreTestData.EV(); }, out var index3);

        service.HandleReservation(new Reservation(EVId: index1, 0, 0.2f, 0.6), stationId: 1);
        service.HandleReservation(new Reservation(EVId: index2, 0, 0.2f, 0.6), stationId: 1);
        service.HandleReservation(new Reservation(EVId: index3, 0, 0.2f, 0.6), stationId: 1);

        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index1, StationId: 1, TargetSoC: 0.6, Time: 0));
        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index2, StationId: 1, TargetSoC: 0.8, Time: 1));
        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index3, StationId: 1, TargetSoC: 0.8, Time: 2));

        // Only ev1 should have an EndCharging scheduled — ev2 and ev3 are queued
        var firstEnd = AsEndCharging(scheduler.GetNextEvent());
        Assert.Equal(index1, firstEnd.EVId);
        Assert.Equal(2, charger.Queue.Count);
        Assert.Null(scheduler.GetNextEvent());

        // ev1 finishes — service should start ev2
        service.HandleEndCharging(firstEnd);
        Assert.Single(charger.Queue);
        Assert.IsType<ArriveAtDestination>(scheduler.GetNextEvent());
        var secondEnd = AsEndCharging(scheduler.GetNextEvent());
        Assert.Equal(index2, secondEnd.EVId);
    }

    [Fact]
    public void ThreeEVs_DualCharger_TwoChargeTogetherThirdQueues()
    {
        // Dual charger — first two EVs fill both sides, third queues.
        // After one finishes, third should start and power is redistributed.
        var (service, scheduler, evStore, charger) = BuildDual(maxPowerKW: 200);

        evStore.TryAllocate((_, ref e) => { e = CoreTestData.EV(); }, out var index1);
        evStore.TryAllocate((_, ref e) => { e = CoreTestData.EV(); }, out var index2);
        evStore.TryAllocate((_, ref e) => { e = CoreTestData.EV(); }, out var index3);

        service.HandleReservation(new Reservation(EVId: index1, 0, 0.2f, 0.6), stationId: 1);
        service.HandleReservation(new Reservation(EVId: index2, 0, 0.2f, 0.6), stationId: 1);
        service.HandleReservation(new Reservation(EVId: index3, 0, 0.2f, 0.6), stationId: 1);

        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index1, StationId: 1, TargetSoC: 0.8, Time: 0));
        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index2, StationId: 1, TargetSoC: 0.8, Time: 1));
        service.HandleArrivalAtStation(new ArriveAtStation(EVId: index3, StationId: 1, TargetSoC: 0.8, Time: 2));

        // Both sides occupied — ev3 is queued
        var ev1End = AsEndCharging(scheduler.GetNextEvent());
        Assert.Equal(index1, ev1End.EVId);
        Assert.Single(charger.Queue);

        service.HandleEndCharging(ev1End);

        // ev2 rescheduled + ev3 newly scheduled
        Assert.IsType<ArriveAtDestination>(scheduler.GetNextEvent());
        var nextA = AsEndCharging(scheduler.GetNextEvent());
        var nextB = AsEndCharging(scheduler.GetNextEvent());
        Assert.Empty(charger.Queue);

        var ev2Event = nextA.EVId == index2 ? nextA : nextB;
        var ev3Event = nextA.EVId == index3 ? nextA : nextB;

        Assert.Equal(index2, ev2Event.EVId);
        Assert.Equal(index3, ev3Event.EVId);
        Assert.True(ev2Event.Time > ev1End.Time);
    }

    private static (StationService service, EventScheduler scheduler, EVStore evStore, ChargerBase charger) BuildSingle(ushort maxPowerKW = 150)
    {
        var charger = CoreTestData.SingleCharger(1, maxPowerKW: maxPowerKW);
        var station = CoreTestData.Station(1, chargers: [charger]);
        var scheduler = new EventScheduler();
        var stations = new Dictionary<ushort, Station> { [1] = station };
        var evStore = new EVStore(10);
        var service = EngineTestData.StationService(stations, scheduler, evStore);
        return (service, scheduler, evStore, charger);
    }

    private static (StationService service, EventScheduler scheduler, EVStore evStore, ChargerBase charger) BuildDual(ushort maxPowerKW = 150)
    {
        var charger = CoreTestData.DualCharger(1, maxPowerKW: maxPowerKW);
        var station = CoreTestData.Station(1, chargers: [charger]);
        var stations = new Dictionary<ushort, Station> { [1] = station };
        var scheduler = new EventScheduler();
        var evStore = new EVStore(10);
        var service = EngineTestData.StationService(stations, scheduler, evStore);
        return (service, scheduler, evStore, charger);
    }

    private static EndCharging AsEndCharging(Event? e)
    {
        Assert.NotNull(e);
        Assert.IsType<EndCharging>(e);
        return (EndCharging)e;
    }
}
