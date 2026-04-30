namespace Engine.Metrics.Events;

using Core.Routing;
using Core.Shared;

/// <summary>
/// Calculates the dynamic deadline for an EV journey to determine the expected arrival time considering charging and path deviation overhead.
/// </summary>
/// <remarks>
/// The deadline extends the original ETA by incorporating charging overhead and path deviation overhead. 
/// It accounts for the time required to cover the energy deficit above the minimum State of Charge (SoC) floor, 
/// plus a fixed per-stop cost to account for expected queue wait times.
/// <para>
/// Detailed calculation examples: <see href="https://typst.app/project/rlD1dtoav7CoiEjqxAyFCv"/> showcasing 6 different scenarios.
/// </para>
/// </remarks>
public static class DeadlineCalculator
{
    /// <summary>
    /// Fixed average charger power (kw) should probably be adjusted when we're smarter about power distribution).
    /// </summary>
    private const float _averageChargerPowerKw = 200f;

    /// <summary>
    /// Fixed per-stop overhead in milliseconds.
    /// Set to a baseline of expected queue wait totalling 12 minutes (Can be adjusted).
    /// </summary>
    private const float _stopOverheadMs = 12 * 60 * 1000f;

    /// <summary>
    /// Calculates the dynamic deadline for a journey based on the EV's energy state and routing preferences.
    /// </summary>
    /// <param name="journey">The journey whose original ETA and duration are used as the baseline.</param>
    /// <param name="socAtSpawn">State of charge (0..1) when the EV begins its journey.</param>
    /// <param name="socMin">Minimum acceptable SoC the EV must stay above (floor constraint).</param>
    /// <param name="maxPathDeviationKm">Maximum path deviation in kilometers, used to calculate overhead for non-direct routes.</param>
    /// <param name="batteryCapacityKWh">Total battery capacity in kWh.</param>
    /// <param name="journeyEnergyDemandKWh">Total energy the journey will consume in kWh.</param>
    /// <returns>The deadline as an absolute simulation time.</returns>
    public static Time Calculate(
        Journey journey,
        double socAtSpawn,
        double socMin,
        float maxPathDeviationKm,
        double batteryCapacityKWh,
        double journeyEnergyDemandKWh)
    {
        var chargingOverheadMs = ChargingOverhead(
            socAtSpawn, socMin, batteryCapacityKWh, journeyEnergyDemandKWh);

        var deviationOverheadMs = PathDeviationOverhead(
            maxPathDeviationKm, journey.Original.Duration, journey.Original.DistanceKm);

        return journey.Original.Eta + (uint)(chargingOverheadMs + deviationOverheadMs);
    }

    /// <summary>
    /// Calculates the charging overhead in milliseconds, including active charging time and fixed stop penalties.
    /// </summary>
    /// <remarks>
    /// The calculation determines the energy deficit relative to the minimum SoC floor and estimates 
    /// the required charging duration and number of stops.
    /// <para>
    /// <b>Formula:</b><br/>
    /// Energy Deficit: $E_{deficit} = \max(0, E_{journey} - (SoC_{spawn} - SoC_{min}) \cdot C_{bat})$<br/>
    /// Charging Time: $T_{charge} = \frac{E_{deficit}}{P_{avg}} + (N_{stops} \cdot T_{stop})$
    /// </para>
    /// </remarks>
    /// <param name="socAtSpawn">The State of Charge (0.0 to 1.0) at the start of the journey.</param>
    /// <param name="socMin">The minimum allowed State of Charge floor.</param>
    /// <param name="batteryCapacityKWh">Total battery capacity in kWh.</param>
    /// <param name="journeyEnergyDemandKWh">Total energy required for the trip in kWh.</param>
    /// <returns>The total overhead time in milliseconds.</returns>
    private static double ChargingOverhead(
        double socAtSpawn,
        double socMin,
        double batteryCapacityKWh,
        double journeyEnergyDemandKWh)
    {
        var energyAvailableKWh = (socAtSpawn - socMin) * batteryCapacityKWh;
        var deficitKWh = Math.Max(0.0, journeyEnergyDemandKWh - energyAvailableKWh);

        if (deficitKWh <= 0)
            return 0.0;

        var usablePerSessionKWh = (0.8 - socMin) * batteryCapacityKWh;
        var stops = Math.Ceiling(deficitKWh / usablePerSessionKWh);
        var chargeTimeMs = deficitKWh / _averageChargerPowerKw * Time.MillisecondsPerHour;
        var stopOverheadMs = stops * _stopOverheadMs;

        return chargeTimeMs + stopOverheadMs;
    }

    private static double PathDeviationOverhead(float maxPathDeviationKm, Time originalDuration, float originalDistanceKm)
    => maxPathDeviationKm / ((double)originalDistanceKm / originalDuration);
}
