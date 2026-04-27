namespace Engine.test.Services;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Shared;
using Engine.Services.StationServiceHelpers;
using Engine.test.Builders;
using Xunit;

public class ProjectedWaitTimeTest
{
    [Fact]
    public void ReturnsZero_WhenSingleChargerIsFree()
    {
        var integrator = new ChargingIntegrator(1000);
        var charger = new SingleCharger(1, 100, CreateConnectors());
        var chargerHandler = new SingleChargerHandler(charger, integrator, new Engine.Events.EventScheduler(), EngineTestData.MetricsService());
        var (availableAt, _) = chargerHandler.EstimateWaitTime(new Time(10));

        Assert.Equal(new Time(0), availableAt);
    }

    [Fact]
    public void ReturnsZero_WhenDualChargerIsFree()
    {
        var integrator = new ChargingIntegrator(1000);
        var charger = new DualCharger(3, 200, CreateConnectors(200));
        var chargerHandler = new DualChargerHandler(charger, integrator, new Engine.Events.EventScheduler(), EngineTestData.MetricsService());
        var (availableAt, _) = chargerHandler.EstimateWaitTime(new Time(10));

        Assert.Equal(new Time(0), availableAt);
    }

    [Fact]
    public void ReturnsPositiveWait_WhenSingleChargerHasQueuedEv()
    {
        var integrator = new ChargingIntegrator(1000);
        var charger = new SingleCharger(1, 100, CreateConnectors());
        charger.Queue.Enqueue(CreateEv(
            evId: 1,
            currentSoC: 0.20,
            targetSoC: 0.80,
            capacityKWh: 60,
            maxChargeRateKW: 100,
            arrivalTime: new Time(0)));
        var chargerHandler = new SingleChargerHandler(charger, integrator, new Engine.Events.EventScheduler(), EngineTestData.MetricsService());
        var (availableAt, _) = chargerHandler.EstimateWaitTime(new Time(10));

        Assert.True(availableAt > new Time(0));
    }

    [Fact]
    public void ReturnsPositiveWait_WhenDualChargerHasMoreThanTwoQueuedEvs()
    {
        var integrator = new ChargingIntegrator(1000);
        var charger = new DualCharger(3, 200, CreateConnectors(200));
        charger.Queue.Enqueue(CreateEv(evId: 1, currentSoC: 0.20, targetSoC: 0.80, capacityKWh: 60, maxChargeRateKW: 100, arrivalTime: new Time(0)));
        charger.Queue.Enqueue(CreateEv(evId: 2, currentSoC: 0.25, targetSoC: 0.80, capacityKWh: 60, maxChargeRateKW: 100, arrivalTime: new Time(0)));
        charger.Queue.Enqueue(CreateEv(evId: 3, currentSoC: 0.10, targetSoC: 0.80, capacityKWh: 60, maxChargeRateKW: 100, arrivalTime: new Time(0)));
        var chargerHandler = new DualChargerHandler(charger, integrator, new Engine.Events.EventScheduler(), EngineTestData.MetricsService());
        var (availableAt, _) = chargerHandler.EstimateWaitTime(new Time(10));

        Assert.True(availableAt > new Time(0));
    }

    [Fact]
    public void ReturnsZero_WhenDualChargerStillHasOneFreeSide()
    {
        var integrator = new ChargingIntegrator(1000);
        var charger = new DualCharger(3, 200, CreateConnectors(200));
        charger.Queue.Enqueue(CreateEv(
            evId: 1,
            currentSoC: 0.20,
            targetSoC: 0.80,
            capacityKWh: 60,
            maxChargeRateKW: 100,
            arrivalTime: new Time(0)));
        var chargerHandler = new DualChargerHandler(charger, integrator, new Engine.Events.EventScheduler(), EngineTestData.MetricsService());
        var (availableAt, _) = chargerHandler.EstimateWaitTime(new Time(0));

        Assert.Equal(new Time(0), availableAt);
    }

    [Fact]
    public void ReturnsSumOfQueuedSingleChargingTimes_WhenSingleChargerHasThreeQueuedEvs()
    {
        var integrator = new ChargingIntegrator(1000);
        var charger = new SingleCharger(1, 400, CreateConnectors());
        var simNow = new Time(10);

        var ev1 = CreateEv(evId: 1, currentSoC: 0.20, targetSoC: 0.80, capacityKWh: 60, maxChargeRateKW: 100, arrivalTime: new Time(0));
        var ev2 = CreateEv(evId: 2, currentSoC: 0.35, targetSoC: 0.90, capacityKWh: 75, maxChargeRateKW: 90, arrivalTime: new Time(0));
        var ev3 = CreateEv(evId: 3, currentSoC: 0.10, targetSoC: 0.70, capacityKWh: 50, maxChargeRateKW: 80, arrivalTime: new Time(0));

        var availableAt = simNow;
        var result1 = integrator.IntegrateSingleToCompletion(availableAt, charger.MaxPowerKW, charger, ev1);
        availableAt = result1.CarA.FinishTime!.Value;
        var result2 = integrator.IntegrateSingleToCompletion(availableAt, charger.MaxPowerKW, charger, ev2);
        availableAt = result2.CarA.FinishTime!.Value;
        var result3 = integrator.IntegrateSingleToCompletion(availableAt, charger.MaxPowerKW, charger, ev3);
        availableAt = result3.CarA.FinishTime!.Value;

        var expectedWaitTime = new Time(availableAt - simNow);

        charger.Queue.Enqueue(ev1);
        charger.Queue.Enqueue(ev2);
        charger.Queue.Enqueue(ev3);
        var chargerHandler = new SingleChargerHandler(charger, integrator, new Engine.Events.EventScheduler(), EngineTestData.MetricsService());
        var (estimateAvailableAt, _) = chargerHandler.EstimateWaitTime(simNow);

        Assert.Equal(expectedWaitTime, estimateAvailableAt);
    }

    [Fact]
    public void ReturnsEarliestFinishOfTwoQueuedEvs_WhenDualChargerHasTwoQueuedEvs()
    {
        var integrator = new ChargingIntegrator(1000);
        var charger = new DualCharger(3, 200, CreateConnectors(200));
        var simNow = new Time(10);

        var ev1 = CreateEv(evId: 1, currentSoC: 0.20, targetSoC: 0.80, capacityKWh: 60, maxChargeRateKW: 100, arrivalTime: new Time(0));
        var ev2 = CreateEv(evId: 2, currentSoC: 0.35, targetSoC: 0.90, capacityKWh: 75, maxChargeRateKW: 90, arrivalTime: new Time(0));

        var result = integrator.IntegrateDualToCompletion(simNow, charger.MaxPowerKW, charger, ev1, ev2);
        var firstAvailableTime = result.CarA.FinishTime!.Value < result.CarB!.FinishTime!.Value
            ? result.CarA.FinishTime.Value
            : result.CarB.FinishTime.Value;
        var expectedWaitTime = new Time(firstAvailableTime - simNow);

        charger.Queue.Enqueue(ev1);
        charger.Queue.Enqueue(ev2);
        var chargerHandler = new DualChargerHandler(charger, integrator, new Engine.Events.EventScheduler(), EngineTestData.MetricsService());
        var (availableAt, _) = chargerHandler.EstimateWaitTime(simNow);

        Assert.Equal(expectedWaitTime, availableAt);
    }

    [Fact]
    public void ReturnsProjectedWait_WhenDualChargerHasThreeQueuedEvs()
    {
        var integrator = new ChargingIntegrator(1000);
        var charger = new DualCharger(3, 200, CreateConnectors(200));
        var simNow = new Time(10);

        var ev1 = CreateEv(1, 0.20, 0.80, 60, 100, new Time(0));
        var ev2 = CreateEv(2, 0.35, 0.90, 75, 90, new Time(0));
        var ev3 = CreateEv(3, 0.10, 0.70, 50, 80, new Time(0));

        var firstRun = integrator.IntegrateDualToCompletion(simNow, charger.MaxPowerKW, charger, ev1, ev2);

        Time expectedWaitTime;
        if (firstRun.CarA.FinishTime!.Value <= firstRun.CarB!.FinishTime!.Value)
        {
            var ev2Remaining = ev2 with { CurrentSoC = firstRun.CarA.PartnerSoCAtFinish };
            var secondRun = integrator.IntegrateDualToCompletion(firstRun.CarA.FinishTime.Value, charger.MaxPowerKW, charger, ev3, ev2Remaining);
            var nextAvailableTime = secondRun.CarA.FinishTime!.Value < secondRun.CarB!.FinishTime!.Value
                ? secondRun.CarA.FinishTime.Value
                : secondRun.CarB.FinishTime.Value;
            expectedWaitTime = nextAvailableTime - simNow;
        }
        else
        {
            var ev1Remaining = ev1 with { CurrentSoC = firstRun.CarB.PartnerSoCAtFinish };
            var secondRun = integrator.IntegrateDualToCompletion(firstRun.CarB.FinishTime.Value, charger.MaxPowerKW, charger, ev1Remaining, ev3);
            var nextAvailableTime = secondRun.CarA.FinishTime!.Value < secondRun.CarB!.FinishTime!.Value
                ? secondRun.CarA.FinishTime.Value
                : secondRun.CarB.FinishTime.Value;
            expectedWaitTime = nextAvailableTime - simNow;
        }

        charger.Queue.Enqueue(ev1);
        charger.Queue.Enqueue(ev2);
        charger.Queue.Enqueue(ev3);
        var chargerHandler = new DualChargerHandler(charger, integrator, new Engine.Events.EventScheduler(), EngineTestData.MetricsService());
        var (availableAt, _) = chargerHandler.EstimateWaitTime(simNow);

        Assert.Equal(expectedWaitTime, availableAt);
    }

    [Fact]
    public void ReturnsCorrectSchedule_WhenSingleChargerHasQueuedEvs()
    {
        var integrator = new ChargingIntegrator(1000);
        var charger = new SingleCharger(1, 100, CreateConnectors());
        var simNow = new Time(10);

        var ev1 = CreateEv(1, 0.20, 0.80, 60, 100, new Time(0));
        var ev2 = CreateEv(2, 0.35, 0.90, 75, 90, new Time(0));

        var result1 = integrator.IntegrateSingleToCompletion(simNow, charger.MaxPowerKW, charger, ev1);
        var finish1 = result1.CarA.FinishTime!.Value;

        var result2 = integrator.IntegrateSingleToCompletion(finish1, charger.MaxPowerKW, charger, ev2);
        var finish2 = result2.CarA.FinishTime!.Value;

        charger.Queue.Enqueue(ev1);
        charger.Queue.Enqueue(ev2);

        var chargerHandler = new SingleChargerHandler(charger, integrator, new Engine.Events.EventScheduler(), EngineTestData.MetricsService());
        var (_, schedule) = chargerHandler.EstimateWaitTime(simNow);

        Assert.Equal(2, schedule.Count);
        Assert.Equal(1, schedule[0].EVId);
        Assert.Equal(finish1, schedule[0].FinishTime);
        Assert.Equal(2, schedule[1].EVId);
        Assert.Equal(finish2, schedule[1].FinishTime);
        Assert.True(finish1 < finish2);
    }

    [Fact]
    public void ReturnsCorrectSchedule_WhenDualChargerHasQueuedEvs()
    {
        var integrator = new ChargingIntegrator(1000);
        var charger = new DualCharger(3, 200, CreateConnectors(100));
        var simNow = new Time(10);

        // Identical batteries, staggered starting SoCs to guarantee completion order.
        var ev1 = CreateEv(1, currentSoC: 0.80, targetSoC: 1.00, capacityKWh: 100, maxChargeRateKW: 100, arrivalTime: new Time(0));
        var ev2 = CreateEv(2, currentSoC: 0.50, targetSoC: 1.00, capacityKWh: 100, maxChargeRateKW: 100, arrivalTime: new Time(0));
        var ev3 = CreateEv(3, currentSoC: 0.10, targetSoC: 1.00, capacityKWh: 100, maxChargeRateKW: 100, arrivalTime: new Time(0));

        var expectedSchedule = new List<(int EVId, Time FinishTime)>();

        var run1 = integrator.IntegrateDualToCompletion(simNow, charger.MaxPowerKW, charger, ev1, ev2);
        expectedSchedule.Add((1, run1.CarA.FinishTime!.Value));

        var ev2Remaining = ev2 with { CurrentSoC = run1.CarA.PartnerSoCAtFinish };
        var run2 = integrator.IntegrateDualToCompletion(run1.CarA.FinishTime.Value, charger.MaxPowerKW, charger, ev3, ev2Remaining);
        expectedSchedule.Add((2, run2.CarB!.FinishTime!.Value));

        var ev3Remaining = ev3 with { CurrentSoC = run2.CarB.PartnerSoCAtFinish };
        var phantom = ev3Remaining with { CurrentSoC = ev3Remaining.TargetSoC };
        var run3 = integrator.IntegrateDualToCompletion(run2.CarB.FinishTime.Value, charger.MaxPowerKW, charger, ev3Remaining, phantom);
        expectedSchedule.Add((3, run3.CarA.FinishTime!.Value));

        charger.Queue.Enqueue(ev1);
        charger.Queue.Enqueue(ev2);
        charger.Queue.Enqueue(ev3);

        var chargerHandler = new DualChargerHandler(charger, integrator, new Engine.Events.EventScheduler(), EngineTestData.MetricsService());
        var (_, schedule) = chargerHandler.EstimateWaitTime(simNow);

        Assert.Equal(3, schedule.Count);
        Assert.Equal(expectedSchedule[0], schedule[0]);
        Assert.Equal(expectedSchedule[1], schedule[1]);
        Assert.Equal(expectedSchedule[2], schedule[2]);
        Assert.True(expectedSchedule[0].FinishTime < expectedSchedule[1].FinishTime);
        Assert.True(expectedSchedule[1].FinishTime < expectedSchedule[2].FinishTime);
    }

    private static ConnectedEV CreateEv(
        int evId,
        double currentSoC,
        double targetSoC,
        double capacityKWh,
        double maxChargeRateKW,
        Time arrivalTime)
    {
        return new ConnectedEV(
            EVId: evId,
            CurrentSoC: currentSoC,
            TargetSoC: targetSoC,
            CapacityKWh: capacityKWh,
            MaxChargeRateKW: maxChargeRateKW,
            ArrivalTime: arrivalTime);
    }

    private static Connectors CreateConnectors(ushort powerKW = 100)
    {
        return new Connectors((
            new Connector(powerKW),
            new Connector(powerKW)));
    }
}
