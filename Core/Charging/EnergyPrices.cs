namespace Core.Charging;

/// <summary>
/// Provides estimated EV charging prices in DKK/kWh for each hour of the day (0–23),
/// based on an interpolation between DK1 day-ahead spot prices and the elbiil.dk
/// retail charging band (2.69–5.14 DKK/kWh) for 2026-03-05.
/// </summary>
public static class EnergyPrices
{
    /// <summary>
    /// Array of energy price for each hour.
    /// </summary>
    /// <remarks>
    /// Estimated price based on an interpolation between day-ahead prices and charging prices.
    /// </remarks>
    private static readonly float[] _energyPrices = new float[]
    {
        3.7402f, 3.4516f, 3.4190f, 3.4088f,
        3.4216f, 3.3849f, 3.6301f, 4.5660f,
        4.3741f, 3.6996f, 3.2035f, 2.8069f,
        2.7430f, 2.7241f, 2.7328f, 2.6900f,
        3.1449f, 3.6433f, 5.1400f, 4.6984f,
        3.9832f, 3.9384f, 3.8054f, 3.6926f,
    };

    /// <summary>
    /// Gets the prices from the supplied hour.
    /// </summary>
    /// <param name="hour">The hour being queried.</param>
    /// <returns>The price at supplied hour.</returns>
    public static float GetPrice(int hour)
    {
        if (hour < 0 || hour > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23.");
        return _energyPrices[hour];
    }
}