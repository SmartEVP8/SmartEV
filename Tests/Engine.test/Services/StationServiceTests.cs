namespace Engine.test.Services;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Charging.ChargingModel.Chargepoint;
using Core.Shared;
using Core.Vehicles;
using Engine.Events;
using Engine.Services;

public class StationServiceTests
{
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

        var station = new Station(1, "Test", "Test Address",
            new Position(0, 0), [charger], new Random(42), MakeEnergyPrices());

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

        var station = new Station(1, "Test", "Test Address",
            new Position(0, 0), [charger], new Random(42), MakeEnergyPrices());

        var scheduler = new EventScheduler();
        var integrator = new ChargingIntegrator(stepSeconds: 60);
        var service = new StationService([station], integrator, scheduler);
        return (service, scheduler);
    }

    private static ConnectedCar MakeCar(uint evId, double currentSoC, double targetSoC,
        Socket socket = Socket.CCS2)
    {
        var model = EVModels.Models.First(m => m.Model == "Volkswagen ID.3");
        return new ConnectedCar(
            CarId: (int)evId,
            CurrentSoC: currentSoC,
            TargetSoC: targetSoC,
            CapacityKWh: model.BatteryConfig.MaxCapacityKWh,
            MaxChargeRateKW: model.BatteryConfig.ChargeRateKW,
            Socket: socket);
    }

    private static EndCharging AsEndCharging(IEvent? e)
    {
        Assert.NotNull(e);
        Assert.IsType<EndCharging>(e);
        return (EndCharging)e!;
    }

    // ── Two cars charge simultaneously on dual charger ────────

    [Fact]
    public void TwoCars_DualCharger_BothReceiveCharge()
    {
        // Two cars arrive at a dual charger simultaneously.
        // Both should start charging and have EndCharging events scheduled.
        var (service, scheduler) = BuildDual();

        var car1 = MakeCar(1, currentSoC: 0.2, targetSoC: 0.8);
        var car2 = MakeCar(2, currentSoC: 0.2, targetSoC: 0.8);

        service.HandleArrivalAtStation(new ArriveAtStation(1, 1, 0), car1);
        service.HandleArrivalAtStation(new ArriveAtStation(2, 1, 0), car2);

        var end1 = AsEndCharging(scheduler.GetNextEvent());
        var end2 = AsEndCharging(scheduler.GetNextEvent());

        // Both cars are charging — different EVIds, same charger
        Assert.NotEqual(end1.EVId, end2.EVId);
        Assert.Equal(end1.ChargerId, end2.ChargerId);

        // Finish times should be in the future
        Assert.True(end1.Time > 0);
        Assert.True(end2.Time > 0);
    }

    // ── Queue: third EV waits, starts after first finishes ───

    [Fact]
    public void ThreeEVs_SingleCharger_ThirdQueuesAndStartsAfterFirst()
    {
        // Single charger — first EV starts immediately, second and third queue.
        // After first finishes, second should start.
        var (service, scheduler) = BuildSingle();

        var car1 = MakeCar(1, currentSoC: 0.5, targetSoC: 0.6); // small delta — finishes quickly
        var car2 = MakeCar(2, currentSoC: 0.2, targetSoC: 0.8);
        var car3 = MakeCar(3, currentSoC: 0.2, targetSoC: 0.8);

        service.HandleArrivalAtStation(new ArriveAtStation(1, 1, 0), car1);
        service.HandleArrivalAtStation(new ArriveAtStation(2, 1, 0), car2);
        service.HandleArrivalAtStation(new ArriveAtStation(3, 1, 0), car3);

        // Only car1 should have an EndCharging scheduled — car2 and car3 are queued
        var firstEnd = AsEndCharging(scheduler.GetNextEvent());
        Assert.Equal(1u, firstEnd.EVId);
        Assert.Null(scheduler.GetNextEvent()); // car2 and car3 still queued

        // car1 finishes — service should start car2
        service.HandleEndCharging(firstEnd);

        // car2 should now be scheduled
        var secondEnd = AsEndCharging(scheduler.GetNextEvent());
        Assert.Equal(2u, secondEnd.EVId);
    }

    // ── Dual charger queue and power distribution ─────────────

    [Fact]
    public void ThreeEVs_DualCharger_TwoChargeTogetherThirdQueues()
    {
        // Dual charger — first two EVs fill both sides, third queues.
        // After one finishes, third should start and power is redistributed.
        var (service, scheduler) = BuildDual(maxPowerKW: 200);

        var car1 = MakeCar(1, currentSoC: 0.7, targetSoC: 0.8); // small delta — finishes first
        var car2 = MakeCar(2, currentSoC: 0.2, targetSoC: 0.8);
        var car3 = MakeCar(3, currentSoC: 0.2, targetSoC: 0.8);

        service.HandleArrivalAtStation(new ArriveAtStation(1, 1, 0), car1);
        service.HandleArrivalAtStation(new ArriveAtStation(2, 1, 0), car2);
        service.HandleArrivalAtStation(new ArriveAtStation(3, 1, 0), car3);

        // Both sides occupied — car3 is queued
        var end1 = AsEndCharging(scheduler.GetNextEvent());
        var end2 = AsEndCharging(scheduler.GetNextEvent());
        Assert.Null(scheduler.GetNextEvent()); // car3 queued, not scheduled yet

        // car1 finishes — service handles it internally
        // Both sides occupied — car1 finishes first (small delta)
        Console.WriteLine($"Queue count before GetNextEvent: {scheduler.QueueCount}");

        // Both sides occupied — car3 is queued
        // Only ONE event should be dequeued here to verify car1 finishes first
        // car1 finishes first (small delta) — just get that one event
        var car1End = AsEndCharging(scheduler.GetNextEvent()); // t=180
        Assert.Equal(1u, car1End.EVId);

        // car2's event (t=960) is still in the queue, car3 not yet scheduled
        Assert.NotNull(scheduler.PeekNextEvent());
        Assert.Equal(1, scheduler.QueueCount); // exactly one event pending

        service.HandleEndCharging(car1End);

        // car2 rescheduled + car3 newly scheduled
        var nextA = AsEndCharging(scheduler.GetNextEvent());
        var nextB = AsEndCharging(scheduler.GetNextEvent());

        var car2Event = nextA.EVId == 2u ? nextA : nextB;
        var car3Event = nextA.EVId == 3u ? nextA : nextB;

        Assert.Equal(2u, car2Event.EVId);
        Assert.Equal(3u, car3Event.EVId);
        Assert.True(car2Event.Time > car1End.Time);
    }
}