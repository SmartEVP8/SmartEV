using Core.Shared;
namespace Core.Vehicles;

/// <summary>
/// Provides a method to calculate the urgency of charging based on the state of charge (SoC) of the battery.
/// </summary>
/// <remarks>
/// The urgency is calculated using an inverse-style curve, where the urgency is high when the SoC
/// is low and decreases as the SoC increases. A small constant is added to the denominator to avoid
/// division by zero when the SoC is near 0.
/// </remarks>
public static class Urgency
{
    /// <summary>
    /// Calculates the urgency of charging based on the state of charge (SoC) of the battery at arrival at
    /// and a minimum acceptable charge level.
    /// </summary>
    /// <param name="ev">The EV for which to calculate urgency.</param>
    /// <param name="durationToStation">The estimated duration to reach the station, used to estimate SoC at arrival.</param>
    /// <returns>
    /// The urgency of charging as a value between 0 and 1, where a higher value indicates a more urgent need for charging.
    /// </returns>
    public static double CalculateChargeUrgency(ref EV ev, Time durationToStation)
    {
        const double upperChargeLimit = 0.80;

        var distanceToStation = ev.DistanceEVCanDriveInTime(durationToStation);
        var soc = (ev.Battery.CurrentChargeKWh - ev.EnergyForDistanceKWh(distanceToStation)) / ev.Battery.MaxCapacityKWh;

        if (soc <= 0)
            return 1 - float.MaxValue;

        if (soc >= upperChargeLimit)
            return 0.0;

        if (soc <= ev.Preferences.MinAcceptableCharge)
            return 0.9999;

        return 1.56 * Math.Pow(soc, 2);
    }

    /// <summary>
    /// Determines whether the search for a charging station should be stopped based on the remaining distance.
    /// </summary>
    /// <param name="remainingDistanceKm">The remaining distance on the route in kilometers.</param>
    /// <param name="stopSearchDistanceKm">The distance in kilometers at which to stop searching for a charging station.</param>
    /// <returns>True if the search should be stopped; otherwise, false.</returns>
    public static bool ShouldStopSearching(double remainingDistanceKm, double stopSearchDistanceKm)
        => remainingDistanceKm <= stopSearchDistanceKm;
}
