namespace Core.Charging.ChargingModel;

using Core.Shared;
using System;
using System.Collections.Generic;

/// <summary>
/// An EV currently connected to a connector with everything needed
/// to plan or update its charging session.
/// </summary>
public record ConnectedEV(
    int EVId,
    double CurrentSoC,
    double TargetSoC,
    double CapacityKWh,
    double MaxChargeRateKW,
    Time ArrivalTime);

/// <summary>
/// Contains the integration results specific to a single vehicle in the session.
/// </summary>
/// <param name="Soc">SoC the car reached at the end of the run.</param>
/// <param name="FinishTime">Simulation timestamp (seconds) when the car hit TargetSoC.</param>
/// <param name="EnergyDeliveredKWh">Exact energy delivered to the car during this run.</param>
/// <param name="PartnerSoCAtFinish">SoC of the other connected car at the moment this car finished (or final SoC of the other car if this one never finished first).</param>
/// <param name="CumulativeEnergy">Cumulative energy delivered to this car at each step of the integration run.</param>
public record VehicleIntegrationResult(
    double Soc,
    Time? FinishTime,
    double EnergyDeliveredKWh,
    double PartnerSoCAtFinish,
    List<double> CumulativeEnergy
);

/// <summary>
/// Returned by all integrator methods.
/// </summary>
/// <param name="CarA">Integration result for the first car.</param>
/// <param name="CarB">Integration result for the second car, or null if only one car is integrated.</param>
/// <param name="WastedEnergyKWh">Energy the charger could have delivered but no car absorbed.</param>
/// <param name="DurationMilliseconds">Time duration for this integration run.</param>
/// <param name="StepSeconds">The time step used for this integration run.</param>
public record IntegrationResult(
    VehicleIntegrationResult CarA,
    VehicleIntegrationResult? CarB,
    double WastedEnergyKWh,
    double DurationMilliseconds,
    uint StepSeconds
)
{
    /// <summary>
    /// Gets the total energy delivered to all connected cars during this run.
    /// </summary>
    public double TotalEnergyKWh => CarA.EnergyDeliveredKWh + (CarB?.EnergyDeliveredKWh ?? 0.0);

    public VehicleIntegrationResult? GetResultFor(ChargingSide? side) => side switch
    {
        ChargingSide.Left => CarA,
        ChargingSide.Right => CarB,
        _ => null
    };
}

/// <summary>
/// Performs time integration of charging sessions, given a charging point and connected cars.
/// </summary>
/// <param name="stepSeconds">The time step in seconds. Smaller steps yield more accurate results.</param>
public sealed class ChargingIntegrator(uint stepSeconds)
{
    private readonly Time _stepSeconds = stepSeconds;

    /// <summary>
    /// Integrates the charging session of a single car until it reaches its target SoC.
    /// </summary>
    /// <param name="simNow">The current simulation time in seconds.</param>
    /// <param name="maxKW">The maximum power output of the charger in kilowatts.</param>
    /// <param name="charger">The single charging point to use for integration.</param>
    /// <param name="ev">The connected EV to integrate for.</param>
    /// <returns>An IntegrationResult containing the details of the integration run.</returns>
    public IntegrationResult IntegrateSingleToCompletion(
        Time simNow,
        double maxKW,
        SingleCharger charger,
        ConnectedEV ev) => IntegrateSingle(simNow, maxKW, charger, ev, runUntilSeconds: null);

    /// <summary>
    /// Integrates the charging sessions of two cars until both reach their target SoC.
    /// </summary>
    /// <param name="simNow">The current simulation time in seconds.</param>
    /// <param name="maxKW">The maximum power output of the charger in kilowatts.</param>
    /// <param name="charger">The dual charging point to use for integration.</param>
    /// <param name="evA">The first connected EV to integrate for.</param>
    /// <param name="evB">The second connected EV to integrate for.</param>
    /// <returns>An IntegrationResult containing the details of the integration run.</returns>
    public IntegrationResult IntegrateDualToCompletion(
        Time simNow,
        double maxKW,
        DualCharger charger,
        ConnectedEV evA,
        ConnectedEV evB) => IntegrateDual(simNow, maxKW, charger, evA, evB, runUntilSeconds: null);

    private IntegrationResult IntegrateSingle(
        Time simNow,
        double maxKW,
        SingleCharger charger,
        ConnectedEV ev,
        Time? runUntilSeconds)
    {
        ValidateConnectedEV(ev);

        var soc = ev.CurrentSoC;
        var targetSoC = ev.TargetSoC;
        Time? finishTime = null;
        var energy = 0.0;
        var cumulativeA = new List<double> { 0.0 };
        var wastedEnergy = 0.0;
        Time t = 0;

        var effectiveMaxKW = Math.Min(maxKW, ev.MaxChargeRateKW);

        while (true)
        {
            var finished = finishTime.HasValue || soc >= targetSoC;

            if (runUntilSeconds.HasValue && t >= runUntilSeconds.Value) break;
            if (!runUntilSeconds.HasValue && finished) break;

            Time step = runUntilSeconds.HasValue
                ? Math.Min(_stepSeconds, runUntilSeconds.Value - t)
                : _stepSeconds;
            var stepHours = (double)step / Time.MillisecondsPerHour;

            if (!finished)
            {
                var power = charger.GetPowerOutput(effectiveMaxKW, soc);
                var delta = power * stepHours;
                var remaining = (targetSoC - soc) * ev.CapacityKWh;

                if (delta >= remaining)
                {
                    energy += remaining;
                    wastedEnergy += delta - remaining;
                    soc = targetSoC;
                    finishTime = simNow + t;
                }
                else
                {
                    energy += delta;
                    wastedEnergy += (effectiveMaxKW * stepHours) - delta;
                    soc += delta / ev.CapacityKWh;
                }
            }

            cumulativeA.Add(energy);
            t += step;
        }

        var carAResult = new VehicleIntegrationResult(
            Soc: soc,
            FinishTime: finishTime,
            EnergyDeliveredKWh: energy,
            PartnerSoCAtFinish: 0.0,
            CumulativeEnergy: cumulativeA);

        return new IntegrationResult(
            CarA: carAResult,
            CarB: null,
            WastedEnergyKWh: wastedEnergy,
            DurationMilliseconds: t,
            StepSeconds: _stepSeconds);
    }

    private IntegrationResult IntegrateDual(
        Time simNow,
        double maxKW,
        DualCharger charger,
        ConnectedEV evA,
        ConnectedEV evB,
        Time? runUntilSeconds)
    {
        ValidateConnectedEV(evA);
        ValidateConnectedEV(evB);

        var socA = evA.CurrentSoC;
        var socB = evB.CurrentSoC;
        var targetSoC_A = evA.TargetSoC;
        var targetSoC_B = evB.TargetSoC;
        Time? finishA = null;
        Time? finishB = null;
        var energyA = 0.0;
        var energyB = 0.0;
        var cumulativeA = new List<double> { 0.0 };
        var cumulativeB = new List<double> { 0.0 };
        var wastedEnergy = 0.0;
        double? bSoCWhenAFinish = null;
        double? aSoCWhenBFinish = null;
        Time t = 0;

        (double newSoc, double delivered, Time? finish) Step(
            double soc, double target, double capacityKWh, double power, double stepHours)
        {
            var delta = power * stepHours;
            var remaining = (target - soc) * capacityKWh;
            if (delta >= remaining)
                return (target, remaining, simNow + t);
            return (soc + (delta / capacityKWh), delta, null);
        }

        while (true)
        {
            var aFinished = finishA.HasValue || socA >= targetSoC_A;
            var bFinished = finishB.HasValue || socB >= targetSoC_B;

            if (runUntilSeconds.HasValue && t >= runUntilSeconds.Value) break;
            if (!runUntilSeconds.HasValue && aFinished && bFinished) break;

            Time step = runUntilSeconds.HasValue
                ? Math.Min(_stepSeconds, runUntilSeconds.Value - t)
                : _stepSeconds;
            var stepHours = (double)step / Time.MillisecondsPerHour;

            var (powerA, powerB) = charger.GetPowerDistribution(
                maxKW,
                aFinished ? targetSoC_A : socA,
                bFinished ? targetSoC_B : socB,
                evA.MaxChargeRateKW,
                evB.MaxChargeRateKW);

            var deliveredA = 0.0;
            var deliveredB = 0.0;

            if (!aFinished)
            {
                var (newSoc, delivered, finish) = Step(socA, targetSoC_A, evA.CapacityKWh, powerA, stepHours);
                if (finish.HasValue) bSoCWhenAFinish = socB;
                socA = newSoc;
                deliveredA = delivered;
                energyA += delivered;
                finishA = finish;
            }

            if (!bFinished)
            {
                var (newSoc, delivered, finish) = Step(socB, targetSoC_B, evB.CapacityKWh, powerB, stepHours);
                if (finish.HasValue) aSoCWhenBFinish = socA;
                socB = newSoc;
                deliveredB = delivered;
                energyB += delivered;
                finishB = finish;
            }

            wastedEnergy += (maxKW * stepHours) - (deliveredA + deliveredB);
            cumulativeA.Add(energyA);
            cumulativeB.Add(energyB);
            t += step;
        }

        var carAResult = new VehicleIntegrationResult(
            Soc: socA,
            FinishTime: finishA,
            EnergyDeliveredKWh: energyA,
            PartnerSoCAtFinish: bSoCWhenAFinish ?? socB,
            CumulativeEnergy: cumulativeA);

        var carBResult = new VehicleIntegrationResult(
            Soc: socB,
            FinishTime: finishB,
            EnergyDeliveredKWh: energyB,
            PartnerSoCAtFinish: aSoCWhenBFinish ?? socA,
            CumulativeEnergy: cumulativeB);

        return new IntegrationResult(
            CarA: carAResult,
            CarB: carBResult,
            WastedEnergyKWh: wastedEnergy,
            DurationMilliseconds: t,
            StepSeconds: _stepSeconds);
    }

    private static void ValidateConnectedEV(ConnectedEV ev)
    {
        if (!double.IsFinite(ev.CurrentSoC) || ev.CurrentSoC is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(ev), $"EV {ev.EVId} has invalid CurrentSoC={ev.CurrentSoC}. Expected finite value in [0,1].");

        if (!double.IsFinite(ev.TargetSoC) || ev.TargetSoC is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(ev), $"EV {ev.EVId} has invalid TargetSoC={ev.TargetSoC}. Expected finite value in [0,1].");
    }
}
