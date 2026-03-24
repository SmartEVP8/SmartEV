namespace Engine.test.Services;

using System.Security.Policy;
using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Charging.ChargingModel.Chargepoint;
using Core.Shared;
using Core.Vehicles;
using Engine.Events;
using Engine.Services;
using Microsoft.VisualBasic;

public class StationServiceTests
{
    private readonly int _evId1 = 1;
    private readonly int _evId2 = 2;
    private readonly int _evId3 = 3;

    private readonly ushort _stationId = 1;

    private readonly Time _time = 0;


    [Fact]
    public void TwoCars_DualCharger_BothReceiveCharge()
    {
        // Two cars arrive at a dual charger simultaneously.
        // Both should start charging and have EndCharging events scheduled.
        var (service, scheduler) = BuildDual();

        var ev1 = MakeEV(1, currentSoC: 0.2, targetSoC: 0.8);
        var ev2 = MakeEV(2, currentSoC: 0.2, targetSoC: 0.8);

        service.HandleArrivalAtStation(new ArriveAtStation(_evId1, _stationId, _time), ev1);
        service.HandleArrivalAtStation(new ArriveAtStation(_evId2, _stationId, _time), ev2);

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
        var (service, scheduler) = BuildSingle();

        var ev1 = MakeEV(1, currentSoC: 0.5, targetSoC: 0.6);
        var ev2 = MakeEV(2, currentSoC: 0.2, targetSoC: 0.8);
        var ev3 = MakeEV(3, currentSoC: 0.2, targetSoC: 0.8);

        service.HandleArrivalAtStation(new ArriveAtStation(_evId1, _stationId, _time), ev1);
        service.HandleArrivalAtStation(new ArriveAtStation(_evId2, _stationId, _time), ev2);
        service.HandleArrivalAtStation(new ArriveAtStation(_evId3, _stationId, _time), ev3);

        // Only ev1 should have an EndCharging scheduled — ev2 and ev3 are queued
        var firstEnd = AsEndCharging(scheduler.GetNextEvent());
        Assert.Equal(1, firstEnd.EVId);
        Assert.Equal(2, service.GetChargerState(_stationId)!.Queue.Count);
        Assert.Null(scheduler.GetNextEvent()); // ev2 and ev3 still queued

        // ev1 finishes — service should start ev2
        service.HandleEndCharging(firstEnd);
        Assert.Single(service.GetChargerState(_stationId)!.Queue); // ev3 still queued

        var secondEnd = AsEndCharging(scheduler.GetNextEvent());
        Assert.Equal(2, secondEnd.EVId);
    }

    [Fact]
    public void ThreeEVs_DualCharger_TwoChargeTogetherThirdQueues()
    {
        // Dual charger — first two EVs fill both sides, third queues.
        // After one finishes, third should start and power is redistributed.
        var (service, scheduler) = BuildDual(maxPowerKW: 200);

        var ev1 = MakeEV(1, currentSoC: 0.7, targetSoC: 0.8);
        var ev2 = MakeEV(2, currentSoC: 0.2, targetSoC: 0.8);
        var ev3 = MakeEV(3, currentSoC: 0.2, targetSoC: 0.8);

        service.HandleArrivalAtStation(new ArriveAtStation(_evId1, _stationId, _time), ev1);
        service.HandleArrivalAtStation(new ArriveAtStation(_evId2, _stationId, _time), ev2);
        service.HandleArrivalAtStation(new ArriveAtStation(_evId3, _stationId, _time), ev3);

        // Both sides occupied — ev3 is queued
        var ev1End = AsEndCharging(scheduler.GetNextEvent());
        Assert.Equal(1, ev1End.EVId);
        Assert.Single(service.GetChargerState(_stationId)!.Queue);

        service.HandleEndCharging(ev1End);

        // ev2 rescheduled + ev3 newly scheduled
        var nextA = AsEndCharging(scheduler.GetNextEvent());
        var nextB = AsEndCharging(scheduler.GetNextEvent());
        Assert.Empty(service.GetChargerState(_stationId)!.Queue);

        var ev2Event = nextA.EVId == 2u ? nextA : nextB;
        var ev3Event = nextA.EVId == 3u ? nextA : nextB;

        Assert.Equal(2, ev2Event.EVId);
        Assert.Equal(3, ev3Event.EVId);
        Assert.True(ev2Event.Time > ev1End.Time);
    }

    private static EnergyPrices MakeEnergyPrices()
    {
        var csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "energy_prices.csv");
        return new EnergyPrices(new FileInfo(csvPath));
    }

    private static (StationService service, EventScheduler scheduler) BuildSingle(
        Socket socket = Socket.CCS2,
        int maxPowerKW = 150)
    {
        var connector = new Connector(socket);
        var connectors = new Connectors([connector]);
        var point = new SingleChargingPoint(connectors);
        var charger = new SingleCharger(1, maxPowerKW, point);

        var station = new Station(
            1,
            "Test",
            "Test Address",
            new Position(0, 0),
            [charger],
            new Random(42),
            MakeEnergyPrices());

        var scheduler = new EventScheduler();
        var integrator = new ChargingIntegrator(stepSeconds: 60);
        var service = new StationService([station], integrator, scheduler);
        return (service, scheduler);
    }

    private static (StationService service, EventScheduler scheduler) BuildDual(
        Socket socket = Socket.CCS2,
        int maxPowerKW = 150)
    {
        var point = new DualChargingPoint(new Connectors([new Connector(socket)]));
        var charger = new DualCharger(1, maxPowerKW, point);

        var station = new Station(
            1,
            "Test",
            "Test Address",
            new Position(0, 0),
            [charger],
            new Random(42),
            MakeEnergyPrices());

        var scheduler = new EventScheduler();
        var integrator = new ChargingIntegrator(stepSeconds: 60);
        var service = new StationService([station], integrator, scheduler);
        return (service, scheduler);
    }

    private static ConnectedEV MakeEV(int evId, double currentSoC, double targetSoC, Socket socket = Socket.CCS2)
    {
        var model = EVModels.Models.First(m => m.Model == "Volkswagen ID.3");
        return new ConnectedEV(
            EVId: evId,
            CurrentSoC: currentSoC,
            TargetSoC: targetSoC,
            CapacityKWh: model.BatteryConfig.MaxCapacityKWh,
            MaxChargeRateKW: model.BatteryConfig.ChargeRateKW,
            Socket: socket);
    }

    private static EndCharging AsEndCharging(Event? e)
    {
        Assert.NotNull(e);
        Assert.IsType<EndCharging>(e);
        return (EndCharging)e!;
    }
}