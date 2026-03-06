namespace Core.Charging;

using System.Collections.Immutable;
using System;

/// <summary>
/// Provides estimated EV charging prices in DKK/kWh for each hour of the day (0–23).
/// </summary>
/// <remarks>
/// based on an interpolation between day-ahead spot prices from energidataservice.dk <see href="https://energidataservice.dk/tso-electricity/DayAheadPrices"/>
/// and elbiil.dk <see href="https://www.elbiil.dk/opladning/opladning-paa-farten"/>.
/// </remarks>
public static class EnergyPrices
{
    /// <summary>
    /// Array of energy price for each hour.
    /// </summary>
    private static readonly ImmutableArray<(DayOfWeek Day, int Hour, float Price)> _energyPriceTable =
        ImmutableArray.Create<(DayOfWeek Day, int Hour, float Price)>(
        (DayOfWeek.Monday, 0, 2.745128f),
        (DayOfWeek.Monday, 1, 2.69f),
        (DayOfWeek.Monday, 2, 2.695873f),
        (DayOfWeek.Monday, 3, 2.701706f),
        (DayOfWeek.Monday, 4, 2.704069f),
        (DayOfWeek.Monday, 5, 2.930299f),
        (DayOfWeek.Monday, 6, 3.536323f),
        (DayOfWeek.Monday, 7, 4.255596f),
        (DayOfWeek.Monday, 8, 4.634244f),
        (DayOfWeek.Monday, 9, 4.448106f),
        (DayOfWeek.Monday, 10, 4.255463f),
        (DayOfWeek.Monday, 11, 3.954901f),
        (DayOfWeek.Monday, 12, 3.85056f),
        (DayOfWeek.Monday, 13, 3.817506f),
        (DayOfWeek.Monday, 14, 3.857761f),
        (DayOfWeek.Monday, 15, 3.991099f),
        (DayOfWeek.Monday, 16, 4.24692f),
        (DayOfWeek.Monday, 17, 5.050722f),
        (DayOfWeek.Monday, 18, 4.735311f),
        (DayOfWeek.Monday, 19, 4.555534f),
        (DayOfWeek.Monday, 20, 4.113881f),
        (DayOfWeek.Monday, 21, 3.905558f),
        (DayOfWeek.Monday, 22, 3.742239f),
        (DayOfWeek.Monday, 23, 3.471784f),
        (DayOfWeek.Tuesday, 0, 3.324774f),
        (DayOfWeek.Tuesday, 1, 3.329483f),
        (DayOfWeek.Tuesday, 2, 3.287009f),
        (DayOfWeek.Tuesday, 3, 3.237415f),
        (DayOfWeek.Tuesday, 4, 3.208613f),
        (DayOfWeek.Tuesday, 5, 3.326719f),
        (DayOfWeek.Tuesday, 6, 3.875304f),
        (DayOfWeek.Tuesday, 7, 4.428477f),
        (DayOfWeek.Tuesday, 8, 4.729025f),
        (DayOfWeek.Tuesday, 9, 4.491061f),
        (DayOfWeek.Tuesday, 10, 4.208929f),
        (DayOfWeek.Tuesday, 11, 3.754932f),
        (DayOfWeek.Tuesday, 12, 3.519359f),
        (DayOfWeek.Tuesday, 13, 3.496796f),
        (DayOfWeek.Tuesday, 14, 3.59803f),
        (DayOfWeek.Tuesday, 15, 3.896732f),
        (DayOfWeek.Tuesday, 16, 4.178374f),
        (DayOfWeek.Tuesday, 17, 5.005204f),
        (DayOfWeek.Tuesday, 18, 5.14f),
        (DayOfWeek.Tuesday, 19, 4.601091f),
        (DayOfWeek.Tuesday, 20, 4.078711f),
        (DayOfWeek.Tuesday, 21, 3.82552f),
        (DayOfWeek.Tuesday, 22, 3.66556f),
        (DayOfWeek.Tuesday, 23, 3.46995f),
        (DayOfWeek.Wednesday, 0, 3.377132f),
        (DayOfWeek.Wednesday, 1, 3.348611f),
        (DayOfWeek.Wednesday, 2, 3.301522f),
        (DayOfWeek.Wednesday, 3, 3.271686f),
        (DayOfWeek.Wednesday, 4, 3.278548f),
        (DayOfWeek.Wednesday, 5, 3.397511f),
        (DayOfWeek.Wednesday, 6, 3.908956f),
        (DayOfWeek.Wednesday, 7, 4.805566f),
        (DayOfWeek.Wednesday, 8, 5.058689f),
        (DayOfWeek.Wednesday, 9, 4.455247f),
        (DayOfWeek.Wednesday, 10, 4.052408f),
        (DayOfWeek.Wednesday, 11, 3.761731f),
        (DayOfWeek.Wednesday, 12, 3.549682f),
        (DayOfWeek.Wednesday, 13, 3.511669f),
        (DayOfWeek.Wednesday, 14, 3.531071f),
        (DayOfWeek.Wednesday, 15, 3.710836f),
        (DayOfWeek.Wednesday, 16, 4.005855f),
        (DayOfWeek.Wednesday, 17, 4.417082f),
        (DayOfWeek.Wednesday, 18, 4.451413f),
        (DayOfWeek.Wednesday, 19, 4.000456f),
        (DayOfWeek.Wednesday, 20, 3.612231f),
        (DayOfWeek.Wednesday, 21, 3.512871f),
        (DayOfWeek.Wednesday, 22, 3.431476f),
        (DayOfWeek.Wednesday, 23, 3.200255f),
        (DayOfWeek.Thursday, 0, 3.038454f),
        (DayOfWeek.Thursday, 1, 3.01856f),
        (DayOfWeek.Thursday, 2, 2.948786f),
        (DayOfWeek.Thursday, 3, 2.864251f),
        (DayOfWeek.Thursday, 4, 2.775407f),
        (DayOfWeek.Thursday, 5, 2.904827f),
        (DayOfWeek.Thursday, 6, 3.026947f),
        (DayOfWeek.Thursday, 7, 3.589457f),
        (DayOfWeek.Thursday, 8, 3.584839f),
        (DayOfWeek.Thursday, 9, 3.58044f),
        (DayOfWeek.Thursday, 10, 3.47369f),
        (DayOfWeek.Thursday, 11, 3.327413f),
        (DayOfWeek.Thursday, 12, 3.182732f),
        (DayOfWeek.Thursday, 13, 3.160316f),
        (DayOfWeek.Thursday, 14, 3.218158f),
        (DayOfWeek.Thursday, 15, 3.423911f),
        (DayOfWeek.Thursday, 16, 3.758878f),
        (DayOfWeek.Thursday, 17, 4.089831f),
        (DayOfWeek.Thursday, 18, 4.287372f),
        (DayOfWeek.Thursday, 19, 4.050577f),
        (DayOfWeek.Thursday, 20, 3.756388f),
        (DayOfWeek.Thursday, 21, 3.541325f),
        (DayOfWeek.Thursday, 22, 3.402313f),
        (DayOfWeek.Thursday, 23, 3.166477f),
        (DayOfWeek.Friday, 0, 2.98329f),
        (DayOfWeek.Friday, 1, 2.999895f),
        (DayOfWeek.Friday, 2, 3.006737f),
        (DayOfWeek.Friday, 3, 3.015311f),
        (DayOfWeek.Friday, 4, 2.961458f),
        (DayOfWeek.Friday, 5, 3.12357f),
        (DayOfWeek.Friday, 6, 3.449024f),
        (DayOfWeek.Friday, 7, 3.937524f),
        (DayOfWeek.Friday, 8, 4.182802f),
        (DayOfWeek.Friday, 9, 3.977013f),
        (DayOfWeek.Friday, 10, 3.662079f),
        (DayOfWeek.Friday, 11, 3.459764f),
        (DayOfWeek.Friday, 12, 3.367047f),
        (DayOfWeek.Friday, 13, 3.40945f),
        (DayOfWeek.Friday, 14, 3.44245f),
        (DayOfWeek.Friday, 15, 3.553813f),
        (DayOfWeek.Friday, 16, 3.867871f),
        (DayOfWeek.Friday, 17, 4.124794f),
        (DayOfWeek.Friday, 18, 4.271203f),
        (DayOfWeek.Friday, 19, 4.02636f),
        (DayOfWeek.Friday, 20, 3.79313f),
        (DayOfWeek.Friday, 21, 3.521619f),
        (DayOfWeek.Friday, 22, 3.377353f),
        (DayOfWeek.Friday, 23, 3.036172f),
        (DayOfWeek.Saturday, 0, 3.051408f),
        (DayOfWeek.Saturday, 1, 2.908786f),
        (DayOfWeek.Saturday, 2, 2.824599f),
        (DayOfWeek.Saturday, 3, 2.782026f),
        (DayOfWeek.Saturday, 4, 2.758313f),
        (DayOfWeek.Saturday, 5, 2.776126f),
        (DayOfWeek.Saturday, 6, 2.742722f),
        (DayOfWeek.Saturday, 7, 2.96511f),
        (DayOfWeek.Saturday, 8, 3.162233f),
        (DayOfWeek.Saturday, 9, 3.319126f),
        (DayOfWeek.Saturday, 10, 3.293893f),
        (DayOfWeek.Saturday, 11, 3.167459f),
        (DayOfWeek.Saturday, 12, 3.004828f),
        (DayOfWeek.Saturday, 13, 2.820452f),
        (DayOfWeek.Saturday, 14, 2.855487f),
        (DayOfWeek.Saturday, 15, 3.073751f),
        (DayOfWeek.Saturday, 16, 3.582979f),
        (DayOfWeek.Saturday, 17, 4.034345f),
        (DayOfWeek.Saturday, 18, 4.271921f),
        (DayOfWeek.Saturday, 19, 4.01142f),
        (DayOfWeek.Saturday, 20, 3.60058f),
        (DayOfWeek.Saturday, 21, 3.382985f),
        (DayOfWeek.Saturday, 22, 3.2706f),
        (DayOfWeek.Saturday, 23, 3.009931f),
        (DayOfWeek.Sunday, 0, 3.249223f),
        (DayOfWeek.Sunday, 1, 3.176974f),
        (DayOfWeek.Sunday, 2, 2.989731f),
        (DayOfWeek.Sunday, 3, 2.986062f),
        (DayOfWeek.Sunday, 4, 2.947563f),
        (DayOfWeek.Sunday, 5, 2.942631f),
        (DayOfWeek.Sunday, 6, 2.878949f),
        (DayOfWeek.Sunday, 7, 2.946334f),
        (DayOfWeek.Sunday, 8, 3.073174f),
        (DayOfWeek.Sunday, 9, 3.11729f),
        (DayOfWeek.Sunday, 10, 3.073253f),
        (DayOfWeek.Sunday, 11, 2.946166f),
        (DayOfWeek.Sunday, 12, 2.841626f),
        (DayOfWeek.Sunday, 13, 2.744549f),
        (DayOfWeek.Sunday, 14, 2.778417f),
        (DayOfWeek.Sunday, 15, 2.951623f),
        (DayOfWeek.Sunday, 16, 3.350956f),
        (DayOfWeek.Sunday, 17, 3.824883f),
        (DayOfWeek.Sunday, 18, 3.877546f),
        (DayOfWeek.Sunday, 19, 3.541959f),
        (DayOfWeek.Sunday, 20, 3.242744f),
        (DayOfWeek.Sunday, 21, 3.14461f),
        (DayOfWeek.Sunday, 22, 3.005876f),
        (DayOfWeek.Sunday, 23, 2.706561f));

    /// <summary>
    /// Gets the prices from the supplied hour.
    /// </summary>
    /// <param name="day">The day being queried.</param>
    /// <param name="hour">The hour being queried.</param>
    /// <returns>The integer price at a given hour.</returns>
    public static float GetHourPrice(DayOfWeek day, int hour)
    {
        if (!Enum.IsDefined(day))
            throw new ArgumentOutOfRangeException(nameof(day), "Invalid day of week.");
        else if (hour < 0 || hour > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23.");

        return _energyPriceTable.First(x => x.Day == day && x.Hour == hour).Price;
    }

    /// <summary>
    /// Gets a dictionary of all prices for a day.
    /// </summary>
    /// <param name="day">The day being queried.</param>
    /// <returns>A dictionary with (hour, price) tuples.</returns>
    public static Dictionary<int, float> GetDayPrice(DayOfWeek day)
    {
        if (!Enum.IsDefined(typeof(DayOfWeek), day))
            throw new ArgumentOutOfRangeException(nameof(day), "Invalid day of week.");

        return _energyPriceTable
            .Where(x => x.Day == day)
            .ToDictionary(x => x.Hour, x => (float)x.Price);
    }
}