namespace Core.test.Charging;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Shared;
using Core.Vehicles;
using Core.Vehicles.Configs;
using Core.test.Builders;

internal static class Make
{
    public static ConnectedEV EV(EVConfig model, double currentSoC, double targetSoC)
        => new(EVId: 1,
               CurrentSoC: currentSoC,
               TargetSoC: targetSoC,
               CapacityKWh: model.BatteryConfig.MaxCapacityKWh,
               MaxChargeRateKW: model.BatteryConfig.ChargeRateKW,
               ArrivalTime: new Time(0));

    public static ConnectedEV EV(
        double currentSoC,
        double targetSoC,
        double capacityKWh,
        double maxChargeRateKW)
        => new(EVId: 1,
               CurrentSoC: currentSoC,
               TargetSoC: targetSoC,
               CapacityKWh: capacityKWh,
               MaxChargeRateKW: maxChargeRateKW,
               ArrivalTime: new Time(0));

    public static SingleCharger SingleCharger(EVConfig model)
    {
        var connectors = MakeConnectors(model.BatteryConfig.ChargeRateKW);
        return new SingleCharger(id: 0, maxPowerKW: model.BatteryConfig.ChargeRateKW, connectors);
    }

    public static DualCharger DualCharger(EVConfig model)
    {
        var connectors = MakeConnectors(model.BatteryConfig.ChargeRateKW);
        return new DualCharger(id: 0, maxPowerKW: model.BatteryConfig.ChargeRateKW, connectors);
    }

    public static DualCharger DualCharger(ushort maxKW)
    {
        var connectors = MakeConnectors(maxKW);
        return new DualCharger(id: 0, maxPowerKW: maxKW, connectors);
    }

    private static Connectors MakeConnectors(ushort maxKW)
        => new((new Connector(maxKW), new Connector(maxKW)));
}

public class ChargingTest
{
    [Fact]
    public void IntegrateSingleToCompletion_NaNTargetSoC_Throws()
    {
        var integrator = new ChargingIntegrator(stepSeconds: 1);
        var ev = new ConnectedEV(
            EVId: 1,
            CurrentSoC: 0.2,
            TargetSoC: double.NaN,
            CapacityKWh: 60,
            MaxChargeRateKW: 100,
            ArrivalTime: 0);
        var evConfig = CoreTestData.EVConfig();
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => integrator.IntegrateSingleToCompletion(
            simNow: 0,
            maxKW: 100,
            charger: Make.SingleCharger(evConfig),
            ev: ev));

        Assert.Contains("TargetSoC", ex.Message);
    }

    [Fact]
    public void IntegrateWithLowerTargetThanCurrentSoCSingle()
    {
        var integrator = new ChargingIntegrator(stepSeconds: 1);
        var ev = new ConnectedEV(
            EVId: 1,
            CurrentSoC: 0.9,
            TargetSoC: 0.5,
            CapacityKWh: 60,
            MaxChargeRateKW: 100,
            ArrivalTime: 0);
        var evConfig = CoreTestData.EVConfig();
        var result = integrator.IntegrateSingleToCompletion(
            simNow: 0,
            maxKW: 100,
            charger: Make.SingleCharger(evConfig),
            ev: ev);

        Assert.Equal(0, result.DurationMilliseconds);
        Assert.NotNull(result.CarA.FinishTime);
    }

    [Fact]
    public void IntegrateWithLowerTargetThanCurrentSoCDual()
    {
        var integrator = new ChargingIntegrator(stepSeconds: 1);
        var ev1 = new ConnectedEV(
            EVId: 1,
            CurrentSoC: 0.9,
            TargetSoC: 0.5,
            CapacityKWh: 60,
            MaxChargeRateKW: 100,
            ArrivalTime: 0);
        var ev2 = new ConnectedEV(
            EVId: 1,
            CurrentSoC: 0.9,
            TargetSoC: 0.5,
            CapacityKWh: 60,
            MaxChargeRateKW: 100,
            ArrivalTime: 0);
        var evConfig = CoreTestData.EVConfig();
        var result = integrator.IntegrateDualToCompletion(
            simNow: 0,
            maxKW: 100,
            charger: Make.DualCharger(evConfig),
            ev1,
            ev2);

        Assert.Equal(0, result.DurationMilliseconds);
        Assert.NotNull(result.CarA.FinishTime);
        Assert.NotNull(result.CarB);
        Assert.NotNull(result.CarB.FinishTime);
    }

    [Fact]
    public void IntegrateSingleToCompletion()
    {
        // Car charges from 5% to 95% through all three AggressiveTaperCurve regions.
        // Expected energy = 0.9 × 100 kWh = 90 kWh
        // Expected duration from analytic integral of dt = capacity/(maxKW*fraction(soc)) dSoC:
        //   Ramp  (0.05→0.10): ∫ 1/(0.4+4s) ds = (ln(0.8)-ln(0.6))/4  ≈ 0.07192 h
        //   Flat  (0.10→0.70): 0.6 / 1.0                               = 0.60000 h
        //   Taper (0.70→0.95): ∫ 1/(1-3(s-0.7)) ds = -ln(0.25)/3      ≈ 0.46210 h
        //   Total ≈ 1.13402 h ≈ 4082 s
        const double maxKW = 100.0;
        const double capacityKWh = 100.0;
        const double expectedEnergyKWh = (0.95 - 0.05) * capacityKWh;

        var rampHours = (Math.Log(0.8) - Math.Log(0.6)) / 4.0;
        var flatHours = 0.60;
        var taperHours = -Math.Log(0.25) / 3.0;
        var expectedDurationSeconds = (rampHours + flatHours + taperHours) * 3600000.0;

        var integrator = new ChargingIntegrator(stepSeconds: 1);
        var ev = Make.EV(currentSoC: 0.05, targetSoC: 0.95, capacityKWh: capacityKWh, maxChargeRateKW: maxKW);
        var evConfig = CoreTestData.EVConfig(new BatteryConfig((ushort)maxKW, (ushort)capacityKWh));
        var result = integrator.IntegrateSingleToCompletion(
            simNow: 0,
            maxKW: maxKW,
            charger: Make.SingleCharger(evConfig),
            ev: ev);

        Assert.Equal(0.95, result.CarA.Soc, precision: 4);
        Assert.Equal(expectedEnergyKWh, result.CarA.EnergyDeliveredKWh, precision: 1);
        Assert.Equal(expectedDurationSeconds, result.DurationMilliseconds, tolerance: 3.0);
        Assert.Equal(expectedEnergyKWh + result.WastedEnergyKWh, maxKW * (result.DurationMilliseconds / 3600000.0), precision: 1);
        Assert.True(
            result.WastedEnergyKWh > 0,
            "car passes through taper region so some energy is wasted");
    }

    [Fact]
    public void IntegrateSingleToCompletion_CarRateLowerThanCharger_LimitsActualPower()
    {
        // Kia Niro: 85 kW charge rate, 65 kWh.
        // Charger offers 150 kW but the car's onboard rate caps it at 85 kW.
        // A charger at 50 kW becomes the bottleneck instead — takes longer.
        // Energy delivered is identical in all cases since SoC delta is the same.
        var niro = EVModels.Models.First(m => m.Model == "Kia Niro");

        var ev = Make.EV(niro, currentSoC: 0.05, targetSoC: 0.95);
        var integrator = new ChargingIntegrator(stepSeconds: 1);
        var charger = Make.SingleCharger(niro);

        var resultLimitedByCarRate = integrator.IntegrateSingleToCompletion(
            simNow: 0, maxKW: 150.0, charger, ev);

        var resultMatchedRate = integrator.IntegrateSingleToCompletion(
            simNow: 0, maxKW: 85.0, charger, ev);

        var resultLimitedByCharger = integrator.IntegrateSingleToCompletion(
            simNow: 0, maxKW: 50.0, charger, ev);

        Assert.Equal(
            resultMatchedRate.DurationMilliseconds,
            resultLimitedByCarRate.DurationMilliseconds,
            tolerance: 1.0);

        Assert.True(
            resultLimitedByCharger.DurationMilliseconds > resultLimitedByCarRate.DurationMilliseconds,
            "charging at 50 kW should take longer than at the car's full 85 kW rate");

        Assert.Equal(
            resultLimitedByCarRate.CarA.EnergyDeliveredKWh + resultLimitedByCarRate.WastedEnergyKWh,
            85.0 * (resultLimitedByCarRate.DurationMilliseconds / 3600000),
            precision: 1);

        Assert.Equal(
            resultLimitedByCharger.CarA.EnergyDeliveredKWh + resultLimitedByCharger.WastedEnergyKWh,
            50.0 * (resultLimitedByCharger.DurationMilliseconds / 3600000),
            precision: 1);

        Assert.Equal(
            resultMatchedRate.CarA.EnergyDeliveredKWh,
            resultLimitedByCarRate.CarA.EnergyDeliveredKWh,
            resultLimitedByCharger.CarA.EnergyDeliveredKWh);
    }

    [Fact]
    public void IntegrateDualToCompletion_DistributionShiftsAcrossCurveRegions()
    {
        const double maxKW = 150.0;

        var tesla = EVModels.Models.First(m => m.Model == "Tesla Model Y");
        var id3 = EVModels.Models.First(m => m.Model == "Volkswagen ID.3");

        var evA = Make.EV(tesla, currentSoC: 0.70, targetSoC: 0.95);
        var evB = Make.EV(id3, currentSoC: 0.05, targetSoC: 0.95);

        var integrator = new ChargingIntegrator(stepSeconds: 1);
        var result = integrator.IntegrateDualToCompletion(
            simNow: 0,
            maxKW: maxKW,
            charger: Make.DualCharger(tesla),
            evA,
            evB);

        Assert.NotNull(result.CarB);
        Assert.Equal(0.95, result.CarA.Soc, precision: 4);
        Assert.Equal(0.95, result.CarB.Soc, precision: 4);

        Assert.True(
            result.CarA.FinishTime < result.CarB.FinishTime,
            "Tesla starts in taper with small delta — should finish before ID.3");

        var expectedTotal = ((0.95 - 0.70) * tesla.BatteryConfig.MaxCapacityKWh)
                          + ((0.95 - 0.05) * id3.BatteryConfig.MaxCapacityKWh);

        Assert.Equal(expectedTotal, result.TotalEnergyKWh, precision: 2);

        var resultId3Alone = integrator.IntegrateSingleToCompletion(
            simNow: 0,
            maxKW: maxKW / 2,
            charger: Make.SingleCharger(id3),
            ev: evB);

        Assert.True(
            result.CarB.FinishTime < resultId3Alone.CarA.FinishTime,
            "ID.3 should finish sooner in dual due to absorbing Tesla's taper surplus");

        Assert.Equal(
            result.CarA.EnergyDeliveredKWh + result.CarB.EnergyDeliveredKWh + result.WastedEnergyKWh,
            maxKW * (result.DurationMilliseconds / 3600000),
            precision: 2);

        var resultId3AloneVsEvA = integrator.IntegrateSingleToCompletion(
            simNow: 0,
            maxKW: maxKW / 2,
            charger: Make.SingleCharger(id3),
            ev: evA);

        Assert.True(
            result.WastedEnergyKWh < resultId3AloneVsEvA.WastedEnergyKWh,
            "dual point wastes less energy than ID.3 alone because Taycan absorbs the surplus");

        Assert.Equal(
            result.TotalEnergyKWh + result.WastedEnergyKWh,
            maxKW * (result.DurationMilliseconds / 3600000),
            precision: 2);
    }

    [Fact]
    public void IntegrateDualToCompletion_LowRateCar_DonatesSurplusToHighRateCar()
    {
        const double maxKW = 200.0;

        var mazda = EVModels.Models.First(m => m.Model == "Mazda MX-30");
        var taycan = EVModels.Models.First(m => m.Model == "Porsche Taycan");

        var evA = Make.EV(mazda, currentSoC: 0.1, targetSoC: 0.9);
        var evB = Make.EV(taycan, currentSoC: 0.1, targetSoC: 0.9);

        var integrator = new ChargingIntegrator(stepSeconds: 1);
        var result = integrator.IntegrateDualToCompletion(
            simNow: 0,
            maxKW: maxKW,
            charger: Make.DualCharger(400),
            evA,
            evB);

        Assert.NotNull(result.CarB);
        Assert.Equal(0.9, result.CarA.Soc, precision: 4);
        Assert.Equal(0.9, result.CarB.Soc, precision: 4);

        Assert.True(
            result.CarB.EnergyDeliveredKWh > result.CarA.EnergyDeliveredKWh,
            "Taycan should receive more energy as Mazda donates its rate-limited surplus");

        var resultTaycanAlone = integrator.IntegrateSingleToCompletion(
            simNow: 0,
            maxKW: maxKW / 2,
            charger: Make.SingleCharger(taycan),
            ev: evB);

        Assert.True(
            result.CarB.FinishTime < resultTaycanAlone.CarA.FinishTime,
            "Taycan should finish sooner in dual due to absorbing Mazda's rate-limited surplus");

        Assert.Equal(
            result.CarA.EnergyDeliveredKWh + result.CarB.EnergyDeliveredKWh + result.WastedEnergyKWh,
            maxKW * (result.DurationMilliseconds / 3600000),
            precision: 2);
    }
}
