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
    /// Calculates the urgency of charging based on the state of charge (SoC) of the battery
    /// and a minimum acceptable charge level.
    /// </summary>
    /// <param name="stateOfCharge">The state of charge (SoC) of the battery, as a value between 0 and 1.</param>
    /// <param name="minAcceptableCharge">The minimum acceptable charge level, as a value between 0 and 1.</param>
    /// <returns>
    /// The urgency of charging as a value between 0 and 1, where a higher value indicates a more urgent need for charging.
    /// </returns>
    public static double CalculateChargeUrgency(float stateOfCharge, float minAcceptableCharge)
    {
        const double upperChargeLimit = 0.80;

        double soc = Math.Clamp(stateOfCharge, 0f, 1f);
        if (soc >= upperChargeLimit)
            return 0.0;

        double minSoc = Math.Clamp(minAcceptableCharge, 0f, 1f);
        if (soc <= minSoc)
            return 1.0;

        return 0.02 * Math.Pow(soc * 100, 2);
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
