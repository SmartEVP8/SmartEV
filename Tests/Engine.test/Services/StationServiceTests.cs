namespace Engine.test.Services;

using Core.Charging;
using Engine.Events;
using Engine.Services;
using Engine.test.Builders;
using Core.test.Builders;
using Core.Vehicles;

public class StationServiceTests
{
    private readonly ushort _stationId = 1;

    [Fact]
    public void TwoCars_DualCharger_BothReceiveCharge()
    {
        var (service, scheduler, evStore, station, _) = BuildDual();

        var ev1 = CoreTestData.EV(id: 1);
        var ev2 = CoreTestData.EV(id: 2);
        evStore[ev1.Id] = ev1;
        evStore[ev2.Id] = ev2;

        service.HandleArrivalAtStation(new ArriveAtStation(ev1, station, 0.8, 0));
        service.HandleArrivalAtStation(new ArriveAtStation(ev2, station, 0.8, 0));

        var end1 = AsEndCharging(scheduler.GetNextEvent());
        var end2 = AsEndCharging(scheduler.GetNextEvent());

        Assert.NotEqual(end1.EV.Id, end2.EV.Id);
        Assert.Equal(end1.Charger.Id, end2.Charger.Id);

        Assert.True(end1.Time > 0);
        Assert.True(end2.Time > 0);
    }

    [Fact]
    public void ThreeEVs_SingleCharger_FirstStartsRemainingQueues()
    {
        var (service, scheduler, evStore, station, charger) = BuildSingle();

        var ev1 = CoreTestData.EV();
        var ev2 = CoreTestData.EV();
        var ev3 = CoreTestData.EV();
        evStore[ev1.Id] = ev1;
        evStore[ev2.Id] = ev2;
        evStore[ev3.Id] = ev3;

        service.HandleReservation(new Reservation(ev1.Id, 0, 0.2f, 0.6), _stationId);
        service.HandleReservation(new Reservation(ev2.Id, 0, 0.2f, 0.6), _stationId);
        service.HandleReservation(new Reservation(ev3.Id, 0, 0.2f, 0.6), _stationId);

        service.HandleArrivalAtStation(new ArriveAtStation(ev1, station, 0.6, 0));
        service.HandleArrivalAtStation(new ArriveAtStation(ev2, station, 0.8, 1));
        service.HandleArrivalAtStation(new ArriveAtStation(ev3, station, 0.8, 2));

        var firstEnd = AsEndCharging(scheduler.GetNextEvent());
        Assert.Equal(ev1.Id, firstEnd.EV.Id);
        Assert.Equal(2, charger.Queue.Count);
        Assert.Null(scheduler.GetNextEvent());

        service.HandleEndCharging(firstEnd);
        Assert.Single(charger.Queue);
        Assert.IsType<ArriveAtDestination>(scheduler.GetNextEvent());
        var secondEnd = AsEndCharging(scheduler.GetNextEvent());
        Assert.Equal(ev2.Id, secondEnd.EV.Id);
    }

    [Fact]
    public void ThreeEVs_DualCharger_TwoChargeTogetherThirdQueues()
    {
        var (service, scheduler, evStore, station, charger) = BuildDual(maxPowerKW: 200);

        var ev1 = CoreTestData.EV();
        var ev2 = CoreTestData.EV();
        var ev3 = CoreTestData.EV();
        evStore[ev1.Id] = ev1;
        evStore[ev2.Id] = ev2;
        evStore[ev3.Id] = ev3;

        service.HandleReservation(new Reservation(ev1.Id, 0, 0.2f, 0.6), _stationId);
        service.HandleReservation(new Reservation(ev2.Id, 0, 0.2f, 0.6), _stationId);
        service.HandleReservation(new Reservation(ev3.Id, 0, 0.2f, 0.6), _stationId);

        service.HandleArrivalAtStation(new ArriveAtStation(ev1, station, 0.8, 0));
        service.HandleArrivalAtStation(new ArriveAtStation(ev2, station, 0.8, 1));
        service.HandleArrivalAtStation(new ArriveAtStation(ev3, station, 0.8, 2));

        var ev1End = AsEndCharging(scheduler.GetNextEvent());
        Assert.Equal(ev1.Id, ev1End.EV.Id);
        Assert.Single(charger.Queue);

        service.HandleEndCharging(ev1End);

        Assert.IsType<ArriveAtDestination>(scheduler.GetNextEvent());
        var nextA = AsEndCharging(scheduler.GetNextEvent());
        var nextB = AsEndCharging(scheduler.GetNextEvent());
        Assert.Empty(charger.Queue);

        var ev2Event = nextA.EV.Id == ev2.Id ? nextA : nextB;
        var ev3Event = nextA.EV.Id == ev3.Id ? nextA : nextB;

        Assert.Equal(ev2.Id, ev2Event.EV.Id);
        Assert.Equal(ev3.Id, ev3Event.EV.Id);
        Assert.True(ev2Event.Time > ev1End.Time);
    }

    private static (StationService service, EventScheduler scheduler, Dictionary<int, EV> evStore, Station station, ChargerBase charger) BuildSingle(ushort maxPowerKW = 150)
    {
        var charger = CoreTestData.SingleCharger(1, maxPowerKW: maxPowerKW);
        var station = CoreTestData.Station(1, chargers: [charger]);
        var scheduler = new EventScheduler();
        var stations = new Dictionary<ushort, Station> { [1] = station };
        var evStore = new Dictionary<int, EV>();
        var service = EngineTestData.StationService(stations, scheduler, evStore);
        return (service, scheduler, evStore, station, charger);
    }

    private static (StationService service, EventScheduler scheduler, Dictionary<int, EV> evStore, Station station, ChargerBase charger) BuildDual(ushort maxPowerKW = 150)
    {
        var charger = CoreTestData.DualCharger(1, maxPowerKW: maxPowerKW);
        var station = CoreTestData.Station(1, chargers: [charger]);
        var scheduler = new EventScheduler();
        var stations = new Dictionary<ushort, Station> { [1] = station };
        var evStore = new Dictionary<int, EV>();
        var service = EngineTestData.StationService(stations, scheduler, evStore);
        return (service, scheduler, evStore, station, charger);
    }

    private static EndCharging AsEndCharging(Event? e)
    {
        Assert.NotNull(e);
        Assert.IsType<EndCharging>(e);
        return (EndCharging)e;
    }
}
