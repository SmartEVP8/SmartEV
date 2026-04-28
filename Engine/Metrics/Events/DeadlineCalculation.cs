namespace Engine.Metrics.Events;

using Core.Routing;
using Core.Shared;

/// <summary>
/// Calculates the dynamic deadline for an EV journey, used for lateness metrics.
///
/// The deadline extends the original ETA by two overhead terms:
/// - Charging overhead: time to acquire the energy deficit above the minimum SoC floor,
///   plus a fixed per-stop cost covering authentication, connection, and expected queue wait.
/// - Path deviation overhead: expected detour time proportional to the EV's deviation tolerance.
///
/// When the EV has sufficient charge and a low deviation tolerance the deadline approaches
/// the original ETA with no extension.
/// </summary>
public static class DeadlineCalculator
{
    /// <summary>
    /// Fixed average effective charger power across the network (kW).
    /// Reflects the mix of charger types in the simulation without requiring per-station resolution.
    /// </summary>
    private const float _averageChargerPowerKw = 200f;

    /// <summary>
    /// Fixed per-stop overhead in milliseconds.
    /// Composed of authentication + connection + disconnection time (~4 min)
    /// and a baseline expected queue wait (~8 min), totalling 12 minutes.
    /// </summary>
    private const float _stopOverheadMs = 12 * 60 * 1000f;

    /// <summary>
    /// Maximum fractional time overhead attributable to path deviation,
    /// applied to the original drive duration.
    /// An EV at maximum deviation tolerance adds at most this fraction on top.
    /// </summary>
    private const float _pathDeviationScaleFactor = 0.15f;

    /// <summary>Minimum path deviation tolerance across the EV population (km).</summary>
    private const float _pathDeviationMinKm = 5f;

    /// <summary>Maximum path deviation tolerance across the EV population (km).</summary>
    private const float _pathDeviationMaxKm = 30f;

    /// <summary>
    /// Calculates the dynamic deadline for a journey based on the EV's energy state and routing preferences.
    /// </summary>
    /// <param name="journey">The journey whose original ETA and duration are used as the baseline.</param>
    /// <param name="socAtSpawn">State of charge (0..1) when the EV begins its journey.</param>
    /// <param name="socMin">Minimum acceptable SoC the EV must stay above (floor constraint).</param>
    /// <param name="socMax">Maximum SoC the charger will fill to (typically 0.9).</param>
    /// <param name="batteryCapacityKWh">Total battery capacity in kWh.</param>
    /// <param name="journeyEnergyDemandKWh">Total energy the journey will consume in kWh.</param>
    /// <param name="maxPathDeviationKm">The EV's maximum acceptable route detour in km (sampled in [5, 30]).</param>
    /// <returns>The deadline as an absolute simulation time.</returns>
    public static Time Calculate(
        Journey journey,
        double socAtSpawn,
        double socMin,
        double socMax,
        double batteryCapacityKWh,
        double journeyEnergyDemandKWh,
        float maxPathDeviationKm)
    {
        var chargingOverheadMs = ChargingOverhead(
            socAtSpawn, socMin, socMax, batteryCapacityKWh, journeyEnergyDemandKWh);

        var deviationOverheadMs = PathDeviationOverhead(
            maxPathDeviationKm, journey.Original.Duration);

        return journey.Original.Eta + (uint)(chargingOverheadMs + deviationOverheadMs);
    }

    /// <summary>
    /// Charging overhead in milliseconds.
    ///
    /// Energy deficit: <c>E_deficit = max(0, E_journey - (SoC_spawn - SoC_min) * C_bat)</c>
    /// Charging time: <c>T_charge = E_deficit / P_avg + N_stops * T_stop</c>
    /// where N_stops is the ceiling of the deficit divided by the usable capacity per session.
    /// </summary>
    private static double ChargingOverhead(
        double socAtSpawn,
        double socMin,
        double socMax,
        double batteryCapacityKWh,
        double journeyEnergyDemandKWh)
    {
        var energyAvailableKWh = (socAtSpawn - socMin) * batteryCapacityKWh;
        var deficitKWh = Math.Max(0.0, journeyEnergyDemandKWh - energyAvailableKWh);

        if (deficitKWh <= 0)
            return 0.0;

        var usablePerSessionKWh = (socMax - socMin) * batteryCapacityKWh;
        var stops = Math.Ceiling(deficitKWh / usablePerSessionKWh);

        var chargeTimeMs = deficitKWh / _averageChargerPowerKw * Time.MillisecondsPerHour;
        var stopOverheadMs = stops * _stopOverheadMs;

        return chargeTimeMs + stopOverheadMs;
    }

    /// <summary>
    /// Path deviation overhead in milliseconds.
    ///
    /// <c>T_dev = gamma * ((delta - delta_min) / (delta_max - delta_min)) * T_expected</c>
    ///
    /// An EV at the minimum tolerance contributes zero; one at maximum contributes
    /// <c>gamma * T_expected</c>. Tied to drive duration so longer journeys scale proportionally.
    /// </summary>
    private static double PathDeviationOverhead(float maxPathDeviationKm, Time originalDuration)
    {
        if (maxPathDeviationKm <= _pathDeviationMinKm)
            return 0.0;

        var normalized = Math.Clamp(
            (maxPathDeviationKm - _pathDeviationMinKm) / (_pathDeviationMaxKm - _pathDeviationMinKm),
            0.0, 1.0);

        return _pathDeviationScaleFactor * normalized * originalDuration;
    }
}