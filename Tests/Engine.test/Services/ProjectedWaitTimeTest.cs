namespace Engine.test.Services;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Shared;
using Engine.Services.StationServiceHelpers;
using Xunit;

public class ProjectedWaitTimeTest
{
    [Fact]
    public void ReturnsZero_WhenSingleChargerIsFree()
    {
        var integrator = new ChargingIntegrator(1000);
        var estimator = new ProjectedWaitTimeEstimator(integrator);

        var charger = new SingleCharger(1, 100, CreateConnectors());

        var waitTime = estimator.EstimateChargerWaitTime(charger, new Time(10));

        Assert.Equal(new Time(0), waitTime);
    }

    [Fact]
    public void ReturnsZero_WhenDualChargerIsFree()
    {
        var integrator = new ChargingIntegrator(1000);
        var estimator = new ProjectedWaitTimeEstimator(integrator);

        var charger = new DualCharger(3, 200, CreateConnectors(200));

        var waitTime = estimator.EstimateChargerWaitTime(charger, new Time(10));

        Assert.Equal(new Time(0), waitTime);
    }

    [Fact]
    public void ReturnsPositiveWait_WhenSingleChargerHasQueuedEv()
    {
        var integrator = new ChargingIntegrator(1000);
        var estimator = new ProjectedWaitTimeEstimator(integrator);

        var charger = new SingleCharger(1, 100, CreateConnectors());

        charger.Queue.Enqueue((1, CreateEv(
            evId: 1,
            currentSoC: 0.20,
            targetSoC: 0.80,
            capacityKWh: 60,
            maxChargeRateKW: 100,
            arrivalTime: new Time(0))));

        var waitTime = estimator.EstimateChargerWaitTime(charger, new Time(10));

        Assert.True(waitTime > new Time(0));
    }

    [Fact]
    public void ReturnsPositiveWait_WhenDualChargerHasMoreThanTwoQueuedEvs()
    {
        var integrator = new ChargingIntegrator(1000);
        var estimator = new ProjectedWaitTimeEstimator(integrator);

        var charger = new DualCharger(3, 200, CreateConnectors(200));

        charger.Queue.Enqueue((1, CreateEv(
            evId: 1,
            currentSoC: 0.20,
            targetSoC: 0.80,
            capacityKWh: 60,
            maxChargeRateKW: 100,
            arrivalTime: new Time(0))));
        charger.Queue.Enqueue((2, CreateEv(
            evId: 2,
            currentSoC: 0.25,
            targetSoC: 0.80,
            capacityKWh: 60,
            maxChargeRateKW: 100,
            arrivalTime: new Time(0))));
        charger.Queue.Enqueue((3, CreateEv(
            evId: 3,
            currentSoC: 0.10,
            targetSoC: 0.80,
            capacityKWh: 60,
            maxChargeRateKW: 100,
            arrivalTime: new Time(0))));

        var waitTime = estimator.EstimateChargerWaitTime(charger, new Time(10));

        Assert.True(waitTime > new Time(0));
    }

    [Fact]
    public void ReturnsZero_WhenDualChargerStillHasOneFreeSide()
    {
        var integrator = new ChargingIntegrator(1000);
        var estimator = new ProjectedWaitTimeEstimator(integrator);

        var charger = new DualCharger(3, 200, CreateConnectors(200));

        charger.Queue.Enqueue((1, CreateEv(
            evId: 1,
            currentSoC: 0.20,
            targetSoC: 0.80,
            capacityKWh: 60,
            maxChargeRateKW: 100,
            arrivalTime: new Time(0))));

        var waitTime = estimator.EstimateChargerWaitTime(charger, new Time(10));

        Assert.Equal(new Time(0), waitTime);
    }

    [Fact]
    public void ReturnsSumOfQueuedSingleChargingTimes_WhenSingleChargerHasThreeQueuedEvs()
    {
        var integrator = new ChargingIntegrator(1000);
        var estimator = new ProjectedWaitTimeEstimator(integrator);

        var charger = new SingleCharger(1, 400, CreateConnectors());

        var simNow = new Time(10);

        var ev1 = CreateEv(
            evId: 1,
            currentSoC: 0.20,
            targetSoC: 0.80,
            capacityKWh: 60,
            maxChargeRateKW: 100,
            arrivalTime: new Time(0));

        var ev2 = CreateEv(
            evId: 2,
            currentSoC: 0.35,
            targetSoC: 0.90,
            capacityKWh: 75,
            maxChargeRateKW: 90,
            arrivalTime: new Time(0));

        var ev3 = CreateEv(
            evId: 3,
            currentSoC: 0.10,
            targetSoC: 0.70,
            capacityKWh: 50,
            maxChargeRateKW: 80,
            arrivalTime: new Time(0));

        var availableAt = simNow;

        var result1 = integrator.IntegrateSingleToCompletion(
            availableAt,
            charger.MaxPowerKW,
            charger,
            ev1);
        availableAt = result1.FinishTimeA!.Value;

        var result2 = integrator.IntegrateSingleToCompletion(
            availableAt,
            charger.MaxPowerKW,
            charger,
            ev2);
        availableAt = result2.FinishTimeA!.Value;

        var result3 = integrator.IntegrateSingleToCompletion(
            availableAt,
            charger.MaxPowerKW,
            charger,
            ev3);
        availableAt = result3.FinishTimeA!.Value;

        var expectedWaitTime = new Time(availableAt - simNow);

        charger.Queue.Enqueue((1, ev1));
        charger.Queue.Enqueue((2, ev2));
        charger.Queue.Enqueue((3, ev3));

        var actualWaitTime = estimator.EstimateChargerWaitTime(charger, simNow);

        Assert.Equal(expectedWaitTime, actualWaitTime);
    }

    [Fact]
    public void ReturnsEarliestFinishOfTwoQueuedEvs_WhenDualChargerHasTwoQueuedEvs()
    {
        var integrator = new ChargingIntegrator(1000);
        var estimator = new ProjectedWaitTimeEstimator(integrator);

        var charger = new DualCharger(3, 200, CreateConnectors(200));

        var simNow = new Time(10);

        var ev1 = CreateEv(
            evId: 1,
            currentSoC: 0.20,
            targetSoC: 0.80,
            capacityKWh: 60,
            maxChargeRateKW: 100,
            arrivalTime: new Time(0));

        var ev2 = CreateEv(
            evId: 2,
            currentSoC: 0.35,
            targetSoC: 0.90,
            capacityKWh: 75,
            maxChargeRateKW: 90,
            arrivalTime: new Time(0));

        var result = integrator.IntegrateDualToCompletion(
            simNow,
            charger.MaxPowerKW,
            charger,
            ev1,
            ev2);

        var firstAvailableTime = result.FinishTimeA!.Value < result.FinishTimeB!.Value
            ? result.FinishTimeA.Value
            : result.FinishTimeB.Value;

        var expectedWaitTime = new Time(firstAvailableTime - simNow);

        charger.Queue.Enqueue((1, ev1));
        charger.Queue.Enqueue((2, ev2));

        var actualWaitTime = estimator.EstimateChargerWaitTime(charger, simNow);

        Assert.Equal(expectedWaitTime, actualWaitTime);
    }

    [Fact]
    public void ReturnsProjectedWait_WhenDualChargerHasThreeQueuedEvs()
    {
        var integrator = new ChargingIntegrator(1000);
        var estimator = new ProjectedWaitTimeEstimator(integrator);

        var charger = new DualCharger(3, 200, CreateConnectors(200));

        var simNow = new Time(10);

        var ev1 = CreateEv(1, 0.20, 0.80, 60, 100, new Time(0));
        var ev2 = CreateEv(2, 0.35, 0.90, 75, 90, new Time(0));
        var ev3 = CreateEv(3, 0.10, 0.70, 50, 80, new Time(0));

        var firstRun = integrator.IntegrateDualToCompletion(
            simNow,
            charger.MaxPowerKW,
            charger,
            ev1,
            ev2);

        Time expectedWaitTime;

        if (firstRun.FinishTimeA!.Value <= firstRun.FinishTimeB!.Value)
        {
            var ev2Remaining = ev2 with
            {
                CurrentSoC = firstRun.BSoCWhenAFinish,
            };

            var secondRun = integrator.IntegrateDualToCompletion(
                firstRun.FinishTimeA.Value,
                charger.MaxPowerKW,
                charger,
                ev3,
                ev2Remaining);

            var nextAvailableTime = secondRun.FinishTimeA!.Value < secondRun.FinishTimeB!.Value
                ? secondRun.FinishTimeA.Value
                : secondRun.FinishTimeB.Value;

            expectedWaitTime = nextAvailableTime - simNow;
        }
        else
        {
            var ev1Remaining = ev1 with
            {
                CurrentSoC = firstRun.ASoCWhenBFinish,
            };

            var secondRun = integrator.IntegrateDualToCompletion(
                firstRun.FinishTimeB.Value,
                charger.MaxPowerKW,
                charger,
                ev1Remaining,
                ev3);

            var nextAvailableTime = secondRun.FinishTimeA!.Value < secondRun.FinishTimeB!.Value
                ? secondRun.FinishTimeA.Value
                : secondRun.FinishTimeB.Value;

            expectedWaitTime = nextAvailableTime - simNow;
        }

        charger.Queue.Enqueue((1, ev1));
        charger.Queue.Enqueue((2, ev2));
        charger.Queue.Enqueue((3, ev3));

        var actualWaitTime = estimator.EstimateChargerWaitTime(charger, simNow);

        Assert.Equal(expectedWaitTime, actualWaitTime);
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
