namespace Core.Vehicles;

/// <summary>
/// Provides a method to calculate the urgency of charging based on the state of charge (SoC) of the battery.
/// </summary>
/// <remarks>
/// The urgency is calculated using an inverse-style curve, where the urgency is high when the SoC
/// is low and decreases as the SoC increases. A small constant is added to the denominator to avoid
/// division by zero when the SoC is near 0%.
/// </remarks>
public static class Urgency
{
    /// <summary>
    /// Calculates the urgency of charging based on the state of charge (SoC) of the battery 
    /// and a minimum acceptable charge level.
    /// </summary>
    /// <param name="stateOfCharge">
    /// The state of charge (SoC) of the battery, expressed as a percentage.
    /// </param>
    /// <param name="minAcceptableCharge">
    /// The minimum acceptable charge level, expressed as a percentage.
    /// </param>
    /// <remarks>
    /// The urgency is calculated using an inverse-style curve, where the urgency is high when the SoC
    /// is low and decreases as the SoC increases. A small constant is added to the denominator to avoid
    /// division by zero when the SoC is near 0%.
    /// The urgency is normalized to a value between 0 and 1, where 0 indicates no urgency and 1 indicates maximum urgency.
    /// Also note that if the SoC is above a certain upper limit (e.g., 80%), the urgency is set to 0, as charging is not needed.
    /// And if the SoC is below the minimum acceptable charge level, the urgency is set to 1, indicating an urgent need for charging.
    /// The steepness of the curve can be adjusted by changing the exponent used in the calculation, allowing for a more aggressive or more gradual increase in urgency as the SoC decreases.
    /// </remarks>
    /// <returns>
    /// The urgency of charging, where a higher value indicates a more urgent need for charging.
    /// </returns>
    public static double CalculateChargeUrgency(float stateOfCharge, float minAcceptableCharge)
    {
        double soc = Math.Clamp(stateOfCharge, 0f, 100f);
        double normalizedSoc = soc / 100.0;

        const double upperChargeLimit = 0.80;

        if (normalizedSoc >= upperChargeLimit)
        {
            return 0.0;
        }

        double minAcceptableSoc = Math.Clamp(minAcceptableCharge, 0f, 100f);
        double normalizedMinAcceptableSoc = minAcceptableSoc / 100.0;

        if (normalizedSoc <= normalizedMinAcceptableSoc)
        {
            return 1.0;
        }

        const double steepness = 5.0;

        double normalizedPosition =
            (upperChargeLimit - normalizedSoc) /
            (upperChargeLimit - normalizedMinAcceptableSoc);

        double urgency = Math.Pow(normalizedPosition, steepness);

        return Math.Clamp(urgency, 0.0, 1.0);
    }
}