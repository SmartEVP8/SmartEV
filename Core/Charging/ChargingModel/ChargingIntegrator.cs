namespace Core.Charging.ChargingModel;

using Core.Charging.ChargingModel.Chargepoint;
using Core.Shared;

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
    Socket Socket,
    Time ArrivalTime);

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
/// <param name="StepSeconds">The time step used for this integration run.</param>
/// <param name="CumulativeEnergyA">Cummulative energy delivered to A at each step of the integration run.</param>
/// <param name="CumulativeEnergyB">Cummulative energy delivered to B at each step of the integration run.</param>
public record IntegrationResult(
    double SocA,
    double SocB,
    Time? FinishTimeA,
    Time? FinishTimeB,
    double EnergyDeliveredKWhA,
    double EnergyDeliveredKWhB,
    double WastedEnergyKWh,
    double DurationSeconds,
    double ASoCWhenBFinish,
    double BSoCWhenAFinish,
    uint StepSeconds,
    List<double> CumulativeEnergyA,
    List<double> CumulativeEnergyB
    )
{
    /// <summary>
    /// Gets the total energy delivered to both cars during this run.
    /// </summary>
    public double TotalEnergyKWh { get; } = EnergyDeliveredKWhA + EnergyDeliveredKWhB;
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
    /// <param name="point">The single charging point to use for integration.</param>
    /// <param name="ev">The connected EV to integrate for.</param>
    /// <returns>An IntegrationResult containing the details of the integration run.</returns>
    public IntegrationResult IntegrateSingleToCompletion(
        Time simNow,
        double maxKW,
        ISingleChargingPoint point,
        ConnectedEV ev) => IntegrateSingle(simNow, maxKW, point, ev, runUntilSeconds: null);

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
        Time simNow,
        double maxKW,
        IDualChargingPoint point,
        ConnectedEV evA,
        ConnectedEV evB) => IntegrateDual(simNow, maxKW, point, evA, evB, runUntilSeconds: null);

    private IntegrationResult IntegrateSingle(
        Time simNow,
        double maxKW,
        ISingleChargingPoint point,
        ConnectedEV ev,
        Time? runUntilSeconds)
    {
        var soc = ev.CurrentSoC;
        var targetSoC = ev.TargetSoC;
        Time? finishTime = null;
        var energy = 0.0;
        var cummulativeA = new List<double> { 0.0 };
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
            var stepHours = step / 3600.0;

            if (!finished)
            {
                var power = point.GetPowerOutput(effectiveMaxKW, soc);
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

            t += step;
        }

        return new IntegrationResult(
            SocA: soc,
            SocB: 0.0,
            FinishTimeA: finishTime.HasValue ? finishTime.Value : null,
            FinishTimeB: null,
            EnergyDeliveredKWhA: energy,
            EnergyDeliveredKWhB: 0.0,
            WastedEnergyKWh: wastedEnergy,
            DurationSeconds: t,
            BSoCWhenAFinish: 0.0,
            ASoCWhenBFinish: 0.0,
            StepSeconds: _stepSeconds,
            CumulativeEnergyA: cummulativeA,
            CumulativeEnergyB: []);
    }

    private IntegrationResult IntegrateDual(
        Time simNow,
        double maxKW,
        IDualChargingPoint point,
        ConnectedEV evA,
        ConnectedEV evB,
        Time? runUntilSeconds)
    {
        var socA = evA.CurrentSoC;
        var socB = evB.CurrentSoC;
        var targetSoC_A = evA.TargetSoC;
        var targetSoC_B = evB.TargetSoC;
        Time? finishA = null;
        Time? finishB = null;
        var energyA = 0.0;
        var energyB = 0.0;
        var cummulativeA = new List<double> { 0.0 };
        var cummulativeB = new List<double> { 0.0 };
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
            var stepHours = step / 3600.0;

            var (powerA, powerB) = point.GetPowerDistribution(
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
            cummulativeA.Add(energyA);
            cummulativeB.Add(energyB);
            t += step;
        }

        return new IntegrationResult(
            SocA: socA,
            SocB: socB,
            FinishTimeA: finishA,
            FinishTimeB: finishB,
            EnergyDeliveredKWhA: energyA,
            EnergyDeliveredKWhB: energyB,
            WastedEnergyKWh: wastedEnergy,
            DurationSeconds: t,
            BSoCWhenAFinish: bSoCWhenAFinish ?? socB,
            ASoCWhenBFinish: aSoCWhenBFinish ?? socA,
            StepSeconds: _stepSeconds,
            CumulativeEnergyA: cummulativeA,
            CumulativeEnergyB: cummulativeB);
    }
}
