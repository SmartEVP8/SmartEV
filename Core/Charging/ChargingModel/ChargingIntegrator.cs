namespace Core.Charging.ChargingModel;

using Core.Charging.ChargingModel.Chargepoint;
using Core.Shared;

/// <summary>
/// An EV currently connected to a connector with everything needed
/// to plan or update its charging session.
/// </summary>
public record ConnectedEV(
    uint EVId,
    double CurrentSoC,
    double TargetSoC,
    double CapacityKWh,
    double MaxChargeRateKW,
    Socket Socket);

/// <summary>
/// Returned by all integrator methods.
/// </summary>
/// <param name="SocA">SoC each car reached at the end of the run.</param>
/// <param name="SocB">SoC each car reached at the end of the run.</param>
/// <param name="FinishTimeA">Simulation timestamp (seconds) when that car hit TargetSoC.</param>
/// <param name="FinishTimeB">Simulation timestamp (seconds) when that car hit TargetSoC.</param>
/// <param name="EnergyDeliveredKWhA">Exact energy delivered to each car during this run.</param>
/// <param name="EnergyDeliveredKWhB">Exact energy delivered to each car during this run.</param>
/// <param name="WastedEnergyKWh">Energy the charger could have delivered but no car absorbed.</param>
/// <param name="DurationSeconds">Wall time covered by this integration run.</param>
/// <param name="BSoCWhenAFinish">SoC of B at the moment A finished (or final SocB if A never finished first).</param>
/// <param name="ASoCWhenBFinish">SoC of A at the moment B finished (or final SocA if B never finished first).</param>
public record IntegrationResult(
    double SocA,
    double SocB,
    uint? FinishTimeA,
    uint? FinishTimeB,
    double EnergyDeliveredKWhA,
    double EnergyDeliveredKWhB,
    double WastedEnergyKWh,
    double DurationSeconds,
    double ASoCWhenBFinish,
    double BSoCWhenAFinish
    )
{
    /// <summary>
    /// Gets the total energy delivered to both cars during this run.
    /// </summary>
    public double TotalEnergyKWh { get; } = EnergyDeliveredKWhA + EnergyDeliveredKWhB;

    /// <summary>
    /// Returns the utilization of the charger.
    /// </summary>
    /// <param name="maxKW">The maximum power output of the charger in kilowatts.</param>
    /// <returns>The utilization as a value between 0.0 and 1.0.</returns>
    public double Utilization(double maxKW)
    {
        if (DurationSeconds <= 0) return 0.0;
        var maxPossibleKWh = maxKW * (DurationSeconds / 3600.0);
        return TotalEnergyKWh / maxPossibleKWh;
    }
}

/// <summary>
/// Performs time integration of charging sessions, given a charging point and connected cars.
/// </summary>
/// <param name="stepSeconds">The time step in seconds. Smaller steps yield more accurate results.</param>
public sealed class ChargingIntegrator(uint stepSeconds)
{
    private readonly uint _stepSeconds = stepSeconds;

    /// <summary>
    /// Integrates the charging session of a single car until it reaches its target SoC.
    /// </summary>
    /// <param name="simNow">The current simulation time in seconds.</param>
    /// <param name="maxKW">The maximum power output of the charger in kilowatts.</param>
    /// <param name="point">The single charging point to use for integration.</param>
    /// <param name="ev">The connected EV to integrate for.</param>
    /// <returns>An IntegrationResult containing the details of the integration run.</returns>
    public IntegrationResult IntegrateSingleToCompletion(
        uint simNow,
        double maxKW,
        ISingleChargingPoint point,
        ConnectedEV ev)
        => IntegrateSingle(simNow, maxKW, point, ev, runUntilSeconds: null);

    /// <summary>
    /// Integrates the charging sessions of two cars until both reach their target SoC.
    /// </summary>
    /// <param name="simNow">The current simulation time in seconds.</param>
    /// <param name="maxKW">The maximum power output of the charger in kilowatts.</param>
    /// <param name="point">The dual charging point to use for integration.</param>
    /// <param name="evA">The first connected EV to integrate for.</param>
    /// <param name="evB">The second connected EV to integrate for.</param>
    /// <returns>An IntegrationResult containing the details of the integration run.</returns>
    public IntegrationResult IntegrateDualToCompletion(
        uint simNow,
        double maxKW,
        IDualChargingPoint point,
        ConnectedEV evA,
        ConnectedEV evB)
        => IntegrateDual(simNow, maxKW, point, evA, evB, runUntilSeconds: null);

    private IntegrationResult IntegrateSingle(
        uint simNow,
        double maxKW,
        ISingleChargingPoint point,
        ConnectedEV ev,
        uint? runUntilSeconds)
    {
        var soc = ev.CurrentSoC;
        double? finishTime = null;
        var energy = 0.0;
        var wastedEnergy = 0.0;
        uint t = 0;

        var effectiveMaxKW = Math.Min(maxKW, ev.MaxChargeRateKW);

        while (true)
        {
            var finished = finishTime.HasValue || soc >= ev.TargetSoC;

            if (runUntilSeconds.HasValue && t >= runUntilSeconds.Value) break;
            if (!runUntilSeconds.HasValue && finished) break;

            var step = runUntilSeconds.HasValue
                ? Math.Min(_stepSeconds, runUntilSeconds.Value - t)
                : _stepSeconds;
            var stepHours = step / 3600.0;

            if (!finished)
            {
                var power = point.GetPowerOutput(effectiveMaxKW, soc);
                var delta = power * stepHours;
                var remaining = (ev.TargetSoC - soc) * ev.CapacityKWh;

                if (delta >= remaining)
                {
                    energy += remaining;
                    wastedEnergy += delta - remaining;
                    soc = ev.TargetSoC;
                    finishTime = simNow + t;
                }
                else
                {
                    energy += delta;
                    wastedEnergy += (effectiveMaxKW * stepHours) - delta;
                    soc += delta / ev.CapacityKWh;
                }
            }

            t += step;
        }

        return new IntegrationResult(
            SocA: soc,
            SocB: 0.0,
            FinishTimeA: finishTime.HasValue ? (uint)finishTime.Value : null,
            FinishTimeB: null,
            EnergyDeliveredKWhA: energy,
            EnergyDeliveredKWhB: 0.0,
            WastedEnergyKWh: wastedEnergy,
            DurationSeconds: t,
            BSoCWhenAFinish: 0.0,
            ASoCWhenBFinish: 0.0);
    }

    private IntegrationResult IntegrateDual(
        uint simNow,
        double maxKW,
        IDualChargingPoint point,
        ConnectedEV evA,
        ConnectedEV evB,
        uint? runUntilSeconds)
    {
        var socA = evA.CurrentSoC;
        var socB = evB.CurrentSoC;
        double? finishA = null;
        double? finishB = null;
        var energyA = 0.0;
        var energyB = 0.0;
        var wastedEnergy = 0.0;
        double? bSoCWhenAFinish = null;
        double? aSoCWhenBFinish = null;
        uint t = 0;

        while (true)
        {
            var aFinished = finishA.HasValue || socA >= evA.TargetSoC;
            var bFinished = finishB.HasValue || socB >= evB.TargetSoC;

            if (runUntilSeconds.HasValue && t >= runUntilSeconds.Value) break;
            if (!runUntilSeconds.HasValue && aFinished && bFinished) break;

            var step = runUntilSeconds.HasValue
                ? Math.Min(_stepSeconds, runUntilSeconds.Value - t)
                : _stepSeconds;
            var stepHours = step / 3600.0;

            var (powerA, powerB) = point.GetPowerDistribution(
                maxKW,
                aFinished ? evA.TargetSoC : socA,
                bFinished ? evB.TargetSoC : socB,
                evA.MaxChargeRateKW,
                evB.MaxChargeRateKW);

            var deliveredA = 0.0;
            var deliveredB = 0.0;

            if (!aFinished)
            {
                var delta = powerA * stepHours;
                var remaining = (evA.TargetSoC - socA) * evA.CapacityKWh;

                if (delta >= remaining)
                {
                    deliveredA = remaining;
                    energyA += remaining;
                    socA = evA.TargetSoC;
                    finishA = simNow + t;
                    bSoCWhenAFinish = socB;
                }
                else
                {
                    deliveredA = delta;
                    energyA += delta;
                    socA += delta / evA.CapacityKWh;
                }
            }

            if (!bFinished)
            {
                var delta = powerB * stepHours;
                var remaining = (evB.TargetSoC - socB) * evB.CapacityKWh;

                if (delta >= remaining)
                {
                    deliveredB = remaining;
                    energyB += remaining;
                    socB = evB.TargetSoC;
                    finishB = simNow + t;
                    aSoCWhenBFinish = socA;
                }
                else
                {
                    deliveredB = delta;
                    energyB += delta;
                    socB += delta / evB.CapacityKWh;
                }
            }

            wastedEnergy += (maxKW * stepHours) - (deliveredA + deliveredB);

            t += step;
        }

        return new IntegrationResult(
            SocA: socA,
            SocB: socB,
            FinishTimeA: finishA.HasValue ? (uint)finishA.Value : null,
            FinishTimeB: finishB.HasValue ? (uint)finishB.Value : null,
            EnergyDeliveredKWhA: energyA,
            EnergyDeliveredKWhB: energyB,
            WastedEnergyKWh: wastedEnergy,
            DurationSeconds: t,
            BSoCWhenAFinish: bSoCWhenAFinish ?? socB,
            ASoCWhenBFinish: aSoCWhenBFinish ?? socA);
    }
}