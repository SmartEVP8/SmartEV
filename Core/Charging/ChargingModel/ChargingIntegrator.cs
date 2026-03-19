namespace Core.Charging.ChargingModel;

using Core.Charging.ChargingModel.Chargepoint;

/// <summary>
/// A car currently connected to a connector with everything needed
/// to plan or update its charging session.
/// </summary>
public record ConnectedCar(
    int CarId,
    double CurrentSoC,
    double TargetSoC,
    double CapacityKWh,
    double MaxChargeRateKW);

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
public record IntegrationResult(
    double SocA,
    double SocB,
    uint? FinishTimeA,
    uint? FinishTimeB,
    double EnergyDeliveredKWhA,
    double EnergyDeliveredKWhB,
    double WastedEnergyKWh,
    double DurationSeconds)
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
    /// <param name="simNow">The current simulation time.</param>
    /// <param name="maxKW">The maximum power output in kilowatts.</param>
    /// <param name="point">The charging point.</param>
    /// <param name="car">The connected car.</param>
    /// <returns>The integration result.</returns>
    public IntegrationResult IntegrateSingleToCompletion(
        uint simNow,
        double maxKW,
        ISingleChargingPoint point,
        ConnectedCar car)
        => IntegrateSingle(simNow, maxKW, point, car, runUntilSeconds: null);

    /// <summary>
    /// Integrates the charging sessions of two cars until both reach their target SoC.
    /// </summary>
    /// <param name="simNow">The current simulation time.</param>
    /// <param name="maxKW">The maximum power output in kilowatts.</param>
    /// <param name="point">The charging point.</param>
    /// <param name="carA">The first connected car.</param>
    /// <param name="carB">The second connected car.</param>
    /// <returns>The integration result.</returns>
    public IntegrationResult IntegrateDualToCompletion(
        uint simNow,
        double maxKW,
        IDualChargingPoint point,
        ConnectedCar carA,
        ConnectedCar carB)
        => IntegrateDual(simNow, maxKW, point, carA, carB, runUntilSeconds: null);

    private IntegrationResult IntegrateSingle(
        uint simNow,
        double maxKW,
        ISingleChargingPoint point,
        ConnectedCar car,
        uint? runUntilSeconds)
    {
        var soc = car.CurrentSoC;
        double? finishTime = null;
        var energy = 0.0;
        var wastedEnergy = 0.0;
        uint t = 0;

        var effectiveMaxKW = Math.Min(maxKW, car.MaxChargeRateKW);

        while (true)
        {
            var finished = finishTime.HasValue || soc >= car.TargetSoC;

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
                var remaining = (car.TargetSoC - soc) * car.CapacityKWh;

                if (delta >= remaining)
                {
                    energy += remaining;
                    wastedEnergy += delta - remaining;
                    soc = car.TargetSoC;
                    finishTime = simNow + t;
                }
                else
                {
                    energy += delta;
                    wastedEnergy += (effectiveMaxKW * stepHours) - delta;
                    soc += delta / car.CapacityKWh;
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
            DurationSeconds: t);
    }

    private IntegrationResult IntegrateDual(
        uint simNow,
        double maxKW,
        IDualChargingPoint point,
        ConnectedCar carA,
        ConnectedCar carB,
        uint? runUntilSeconds)
    {
        var socA = carA.CurrentSoC;
        var socB = carB.CurrentSoC;
        double? finishA = null;
        double? finishB = null;
        var energyA = 0.0;
        var energyB = 0.0;
        var wastedEnergy = 0.0;
        uint t = 0;

        while (true)
        {
            var aFinished = finishA.HasValue || socA >= carA.TargetSoC;
            var bFinished = finishB.HasValue || socB >= carB.TargetSoC;

            if (runUntilSeconds.HasValue && t >= runUntilSeconds.Value) break;
            if (!runUntilSeconds.HasValue && aFinished && bFinished) break;

            var step = runUntilSeconds.HasValue
                ? Math.Min(_stepSeconds, runUntilSeconds.Value - t)
                : _stepSeconds;
            var stepHours = step / 3600.0;

            var (powerA, powerB) = point.GetPowerDistribution(
                maxKW,
                aFinished ? carA.TargetSoC : socA,
                bFinished ? carB.TargetSoC : socB,
                carA.MaxChargeRateKW,
                carB.MaxChargeRateKW);

            var deliveredA = 0.0;
            var deliveredB = 0.0;

            if (!aFinished)
            {
                var delta = powerA * stepHours;
                var remaining = (carA.TargetSoC - socA) * carA.CapacityKWh;

                if (delta >= remaining)
                {
                    deliveredA = remaining;
                    energyA += remaining;
                    socA = carA.TargetSoC;
                    finishA = simNow + t;
                }
                else
                {
                    deliveredA = delta;
                    energyA += delta;
                    socA += delta / carA.CapacityKWh;
                }
            }

            if (!bFinished)
            {
                var delta = powerB * stepHours;
                var remaining = (carB.TargetSoC - socB) * carB.CapacityKWh;

                if (delta >= remaining)
                {
                    deliveredB = remaining;
                    energyB += remaining;
                    socB = carB.TargetSoC;
                    finishB = simNow + t;
                }
                else
                {
                    deliveredB = delta;
                    energyB += delta;
                    socB += delta / carB.CapacityKWh;
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
            DurationSeconds: t);
    }
}