namespace Core.test.Charging;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Shared;
using Core.Charging.ChargingModel.Chargepoint;
using Core.Vehicles;
using Core.Vehicles.Configs;

internal static class Make
{
    public static ConnectedEV EV(EVConfig model, double currentSoC, double targetSoC)
        => new(EVId: 1,
               CurrentSoC: currentSoC,
               TargetSoC: targetSoC,
               CapacityKWh: model.BatteryConfig.MaxCapacityKWh,
               MaxChargeRateKW: model.BatteryConfig.ChargeRateKW,
               Socket: model.BatteryConfig.Socket,
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
               Socket: Socket.CCS2,
               ArrivalTime: new Time(0));

    public static SingleChargingPoint SinglePoint(EVConfig model)
    {
        var c = new Connector(model.BatteryConfig.Socket);
        var connectors = new Connectors([c]);
        connectors.Activate(c);
        return new SingleChargingPoint(connectors);
    }

    public static SingleChargingPoint SinglePoint(Socket socket)
    {
        var c = new Connector(socket);
        var connectors = new Connectors([c]);
        connectors.Activate(c);
        return new SingleChargingPoint(connectors);
    }

    public static DualChargingPoint DualPoint(EVConfig model)
    {
        var socket = model.BatteryConfig.Socket;
        var point = new DualChargingPoint(new Connectors([new Connector(socket)]));
        point.TryConnect(socket);
        point.TryConnect(socket);
        return point;
    }
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
            Socket: Socket.CCS2,
            ArrivalTime: 0);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => integrator.IntegrateSingleToCompletion(
            simNow: 0,
            maxKW: 100,
            point: Make.SinglePoint(Socket.CCS2),
            ev: ev));

        Assert.Contains("TargetSoC", ex.Message);
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
        var expectedDurationSeconds = (rampHours + flatHours + taperHours) * 3600.0;

        var integrator = new ChargingIntegrator(stepSeconds: 1);
        var ev = Make.EV(currentSoC: 0.05, targetSoC: 0.95, capacityKWh: capacityKWh, maxChargeRateKW: maxKW);

        var result = integrator.IntegrateSingleToCompletion(
            simNow: 0,
            maxKW: maxKW,
            point: Make.SinglePoint(Socket.CCS2),
            ev: ev);

        Assert.Equal(0.95, result.SocA, precision: 4);
        Assert.Equal(expectedEnergyKWh, result.EnergyDeliveredKWhA, precision: 1);
        Assert.Equal(expectedDurationSeconds, result.DurationSeconds, tolerance: 3.0);
        Assert.Equal(expectedEnergyKWh + result.WastedEnergyKWh, maxKW * (result.DurationSeconds / 3600.0), precision: 1);
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
        var point = Make.SinglePoint(niro);

        // Charger at 150 kW — car rate (85 kW) is the bottleneck
        var resultLimitedByCarRate = integrator.IntegrateSingleToCompletion(
            simNow: 0, maxKW: 150.0, point, ev);

        // Charger matches car rate exactly — same outcome expected
        var resultMatchedRate = integrator.IntegrateSingleToCompletion(
            simNow: 0, maxKW: 85.0, point, ev);

        // Charger at 50 kW — charger is the bottleneck, takes longer
        var resultLimitedByCharger = integrator.IntegrateSingleToCompletion(
            simNow: 0, maxKW: 50.0, point, ev);

        // Car rate caps the 150 kW charger — identical to running at exactly 85 kW
        Assert.Equal(
            resultMatchedRate.DurationSeconds,
            resultLimitedByCarRate.DurationSeconds,
            tolerance: 1.0);

        // 50 kW charger takes longer than the car's full 85 kW rate
        Assert.True(
            resultLimitedByCharger.DurationSeconds > resultLimitedByCarRate.DurationSeconds,
            "charging at 50 kW should take longer than at the car's full 85 kW rate");

        Assert.Equal(
            resultLimitedByCarRate.EnergyDeliveredKWhA + resultLimitedByCarRate.WastedEnergyKWh,
            85.0 * (resultLimitedByCarRate.DurationSeconds / 3600.0),
            precision: 1);

        Assert.Equal(
            resultLimitedByCharger.EnergyDeliveredKWhA + resultLimitedByCharger.WastedEnergyKWh,
            50.0 * (resultLimitedByCharger.DurationSeconds / 3600.0),
            precision: 1);

        // Energy delivered is the same regardless — same SoC delta in all three runs
        Assert.Equal(
            resultMatchedRate.EnergyDeliveredKWhA,
            resultLimitedByCarRate.EnergyDeliveredKWhA,
            resultLimitedByCharger.EnergyDeliveredKWhA);
    }

    [Fact]
    public void IntegrateDualToCompletion_DistributionShiftsAcrossCurveRegions()
    {
        // Tesla Model Y: 250 kW charge rate, 75 kWh — starts at 70% (immediately in taper)
        // Volkswagen ID.3: 170 kW charge rate, 58 kWh — starts at 5% (ramp → flat → taper)
        // Tesla enters taper immediately and donates surplus to the ID.3.
        // Tesla has a much smaller SoC delta so finishes first.
        // ID.3 charges faster than it would alone at half power.
        const double maxKW = 150.0;

        var tesla = EVModels.Models.First(m => m.Model == "Tesla Model Y");
        var id3 = EVModels.Models.First(m => m.Model == "Volkswagen ID.3");

        var evA = Make.EV(tesla, currentSoC: 0.70, targetSoC: 0.95);
        var evB = Make.EV(id3, currentSoC: 0.05, targetSoC: 0.95);

        var integrator = new ChargingIntegrator(stepSeconds: 1);
        var result = integrator.IntegrateDualToCompletion(
            simNow: 0,
            maxKW: maxKW,
            Make.DualPoint(tesla),
            evA,
            evB);

        Assert.Equal(0.95, result.SocA, precision: 4);
        Assert.Equal(0.95, result.SocB, precision: 4);

        Assert.True(
            result.FinishTimeA < result.FinishTimeB,
            "Tesla starts in taper with small delta — should finish before ID.3");

        var expectedTotal = ((0.95 - 0.70) * tesla.BatteryConfig.MaxCapacityKWh)
                          + ((0.95 - 0.05) * id3.BatteryConfig.MaxCapacityKWh);

        Assert.Equal(expectedTotal, result.TotalEnergyKWh, precision: 2);

        var resultId3Alone = integrator.IntegrateSingleToCompletion(
            simNow: 0,
            maxKW: maxKW / 2,
            Make.SinglePoint(id3),
            evB);

        Assert.True(
            result.FinishTimeB < resultId3Alone.FinishTimeA,
            "ID.3 should finish sooner in dual due to absorbing Tesla's taper surplus");

        Assert.Equal(
            result.EnergyDeliveredKWhA + result.EnergyDeliveredKWhB + result.WastedEnergyKWh,
            maxKW * (result.DurationSeconds / 3600.0),
            precision: 2);

        var resultid3Alone = integrator.IntegrateSingleToCompletion(
            simNow: 0,
            maxKW: maxKW / 2,
            Make.SinglePoint(id3),
            evA);

        Assert.True(
            result.WastedEnergyKWh < resultid3Alone.WastedEnergyKWh,
            "dual point wastes less energy than ID.3 alone because Taycan absorbs the surplus");

        Assert.Equal(
            result.TotalEnergyKWh + result.WastedEnergyKWh,
            maxKW * (result.DurationSeconds / 3600.0),
            precision: 2);
    }

    [Fact]
    public void IntegrateDualToCompletion_LowRateCar_DonatesSurplusToHighRateCar()
    {
        // Mazda MX-30: 50 kW charge rate, 30 kWh
        // Porsche Taycan: 320 kW charge rate, 97 kWh
        //
        // At maxKW=200, nominal=100 each.
        // physicalCapMazda = min(connectorRating, 50) = 50 kW → surplusMazda = 50 kW every step.
        // Taycan absorbs all surplus → charges at 150 kW vs Mazda's 50 kW.
        // Taycan finishes faster than it would at only 100 kW alone.
        const double maxKW = 200.0;

        var mazda = EVModels.Models.First(m => m.Model == "Mazda MX-30");
        var taycan = EVModels.Models.First(m => m.Model == "Porsche Taycan");

        var evA = Make.EV(mazda, currentSoC: 0.1, targetSoC: 0.9);
        var evB = Make.EV(taycan, currentSoC: 0.1, targetSoC: 0.9);

        var integrator = new ChargingIntegrator(stepSeconds: 1);
        var result = integrator.IntegrateDualToCompletion(
            simNow: 0,
            maxKW: maxKW,
            Make.DualPoint(mazda),
            evA,
            evB);

        Assert.Equal(0.9, result.SocA, precision: 4);
        Assert.Equal(0.9, result.SocB, precision: 4);

        Assert.True(
            result.EnergyDeliveredKWhB > result.EnergyDeliveredKWhA,
            "Taycan should receive more energy as Mazda donates its rate-limited surplus");

        // Taycan should finish sooner than it would alone at half power (100 kW).
        var resultTaycanAlone = integrator.IntegrateSingleToCompletion(
            simNow: 0,
            maxKW: maxKW / 2,
            Make.SinglePoint(taycan),
            evB);

        Assert.True(
            result.FinishTimeB < resultTaycanAlone.FinishTimeA,
            "Taycan should finish sooner in dual due to absorbing Mazda's rate-limited surplus");

        // safety check
        Assert.Equal(
            result.EnergyDeliveredKWhA + result.EnergyDeliveredKWhB + result.WastedEnergyKWh,
            maxKW * (result.DurationSeconds / 3600.0),
            precision: 2);
    }
}