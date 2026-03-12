using Core.Charging.ChargingModel.Chargepoint;

namespace Core.Charging.ChargingModel;

// ─────────────────────────────────────────────────────────────
// DATA
// ─────────────────────────────────────────────────────────────

/// <summary>
/// A car currently connected to a connector with everything needed
/// to plan or update its charging session.
/// </summary>
public record ConnectedCar(
    int CarId,
    double CurrentSoC,    // SoC right now, at this moment
    double TargetSoC,     // SoC the car wants to reach
    double CapacityKWh,   // battery size
    IChargingCurve Curve);        // this car's own charging curve

/// <summary>
/// <para>Returned by all integrator methods.</para>
/// <para>
/// SocA / SocB              — SoC each car reached at the end of the run.
/// FinishTimeA/B            — simulation timestamp (seconds) when that car hit TargetSoC.
///                            null if the car did not finish during this run.
/// EnergyDeliveredKWhA/B    — exact energy delivered to each car during this run.
/// DurationSeconds          — wall time covered by this integration run.
/// Utilization(maxKW)       — fraction of maxKW actually delivered over the run. 1.0 = full power the whole time.
/// </para>
/// </summary>
public record IntegrationResult(double SocA,
                                double SocB,
                                uint? FinishTimeA,
                                uint? FinishTimeB,
                                double EnergyDeliveredKWhA,
                                double EnergyDeliveredKWhB,
                                double DurationSeconds)
{
    public double TotalEnergyKWh => EnergyDeliveredKWhA + EnergyDeliveredKWhB;

    /// <summary>
    /// Fraction of maxKW actually delivered averaged over the run duration.
    /// Accumulate TotalEnergyKWh and DurationSeconds across multiple results
    /// to get utilization over a longer period than a single sub-session.
    /// </summary>
    public double Utilization(double maxKW)
    {
        if (DurationSeconds <= 0) return 0.0;
        var maxPossibleKWh = maxKW * (DurationSeconds / 3600.0);
        return TotalEnergyKWh / maxPossibleKWh;
    }
}


public sealed class ChargingIntegrator(uint stepSeconds)
{
    private readonly uint _stepSeconds = stepSeconds;

    public IntegrationResult IntegrateToCompletion(
        uint simNow,
        double maxKW,
        IChargingPoint chargingPoint,
        ConnectedCar carA,
        ConnectedCar? carB = null)
        => Integrate(simNow, maxKW, chargingPoint, carA, carB, runUntilSeconds: null);

    public IntegrationResult IntegrateForDuration(
        uint simNow,
        double maxKW,
        IChargingPoint chargingPoint,
        ConnectedCar carA,
        ConnectedCar? carB,
        uint runUntilSeconds)
        => Integrate(simNow, maxKW, chargingPoint, carA, carB, runUntilSeconds);

    private IntegrationResult Integrate(
        uint simNow,
        double maxKW,
        IChargingPoint chargingPoint,
        ConnectedCar carA,
        ConnectedCar? carB,
        uint? runUntilSeconds)
    {
        var dual = carB is not null;
        var socA = carA.CurrentSoC;
        var socB = carB?.CurrentSoC ?? 0.0;
        double? finishA = null;
        double? finishB = null;
        var energyA = 0.0;
        var energyB = 0.0;
        uint t = 0;

        while (true)
        {
            var aFinished = finishA.HasValue || socA >= carA.TargetSoC;
            var bFinished = !dual || finishB.HasValue || socB >= carB!.TargetSoC;

            if (runUntilSeconds.HasValue && t >= runUntilSeconds.Value) break;
            if (!runUntilSeconds.HasValue && aFinished && bFinished) break;

            var step = runUntilSeconds.HasValue
                                   ? Math.Min(_stepSeconds, runUntilSeconds.Value - t)
                                   : _stepSeconds;
            var stepHours = step / 3600.0;

            // Finished cars pass their TargetSoC so the other car gets freed power
            var (powerA, powerB) = chargingPoint.GetPowerDistribution(
                maxKW,
                aFinished ? carA.TargetSoC : socA,
                dual ? (bFinished ? carB!.TargetSoC : socB) : null,
                carA.Curve,
                carB?.Curve);

            if (!aFinished)
            {
                var deltaEnergy = powerA * stepHours;
                energyA += deltaEnergy;
                socA += deltaEnergy / carA.CapacityKWh;
                if (socA >= carA.TargetSoC)
                {
                    socA = carA.TargetSoC;
                    finishA = simNow + t;
                }
            }

            if (dual && !bFinished)
            {
                var deltaEnergy = powerB * stepHours;
                energyB += deltaEnergy;
                socB += deltaEnergy / carB!.CapacityKWh;
                if (socB >= carB.TargetSoC)
                {
                    socB = carB.TargetSoC;
                    finishB = simNow + t;
                }
            }

            t += step;
        }

        return new IntegrationResult(
            SocA: socA,
            SocB: socB,
            FinishTimeA: finishA.HasValue ? (uint)finishA.Value : null,
            FinishTimeB: finishB.HasValue ? (uint)finishB.Value : null,
            EnergyDeliveredKWhA: energyA,
            EnergyDeliveredKWhB: energyB,
            DurationSeconds: t);
    }
}
