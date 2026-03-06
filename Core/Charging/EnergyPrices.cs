namespace Core.Charging;

using System.Collections.Immutable;
using DayCycles;

/// <summary>
/// Provides estimated EV charging prices in DKK/kWh for each hour of the day (0–23),
/// based on an interpolation between day-ahead spot prices from energidataservice.dk <see href="https://energidataservice.dk/tso-electricity/DayAheadPrices"/>
/// and elbiil.dk <see href="https://www.elbiil.dk/opladning/opladning-paa-farten"/>.
/// </summary>
public static class EnergyPrices
{
    /// <summary>
    /// Array of energy price for each hour.
    /// </summary>
    /// <remarks>
    /// Estimated price based on an interpolation between day-ahead prices and charging prices.
    /// </remarks>
    private static readonly ImmutableArray<(DaysOfWeek Day, int Hour, float Price)> _energyPriceTable =
        ImmutableArray.Create<(DaysOfWeek Day, int Hour, float Price)>(
        (DaysOfWeek.Monday, 0, 0.554331f),
        (DaysOfWeek.Monday, 1, 0.542770f),
        (DaysOfWeek.Monday, 2, 0.544002f),
        (DaysOfWeek.Monday, 3, 0.545225f),
        (DaysOfWeek.Monday, 4, 0.545721f),
        (DaysOfWeek.Monday, 5, 0.593160f),
        (DaysOfWeek.Monday, 6, 0.720241f),
        (DaysOfWeek.Monday, 7, 0.871070f),
        (DaysOfWeek.Monday, 8, 0.950472f),
        (DaysOfWeek.Monday, 9, 0.911439f),
        (DaysOfWeek.Monday, 10, 0.871043f),
        (DaysOfWeek.Monday, 11, 0.808016f),
        (DaysOfWeek.Monday, 12, 0.786136f),
        (DaysOfWeek.Monday, 13, 0.779204f),
        (DaysOfWeek.Monday, 14, 0.787646f),
        (DaysOfWeek.Monday, 15, 0.815606f),
        (DaysOfWeek.Monday, 16, 0.869251f),
        (DaysOfWeek.Monday, 17, 1.037806f),
        (DaysOfWeek.Monday, 18, 0.971665f),
        (DaysOfWeek.Monday, 19, 0.933966f),
        (DaysOfWeek.Monday, 20, 0.841353f),
        (DaysOfWeek.Monday, 21, 0.797669f),
        (DaysOfWeek.Monday, 22, 0.763421f),
        (DaysOfWeek.Monday, 23, 0.706708f),
        (DaysOfWeek.Tuesday, 0, 0.675880f),
        (DaysOfWeek.Tuesday, 1, 0.676868f),
        (DaysOfWeek.Tuesday, 2, 0.667961f),
        (DaysOfWeek.Tuesday, 3, 0.657561f),
        (DaysOfWeek.Tuesday, 4, 0.651522f),
        (DaysOfWeek.Tuesday, 5, 0.676288f),
        (DaysOfWeek.Tuesday, 6, 0.791324f),
        (DaysOfWeek.Tuesday, 7, 0.907323f),
        (DaysOfWeek.Tuesday, 8, 0.970347f),
        (DaysOfWeek.Tuesday, 9, 0.920447f),
        (DaysOfWeek.Tuesday, 10, 0.861285f),
        (DaysOfWeek.Tuesday, 11, 0.766083f),
        (DaysOfWeek.Tuesday, 12, 0.716684f),
        (DaysOfWeek.Tuesday, 13, 0.711953f),
        (DaysOfWeek.Tuesday, 14, 0.733181f),
        (DaysOfWeek.Tuesday, 15, 0.795818f),
        (DaysOfWeek.Tuesday, 16, 0.854877f),
        (DaysOfWeek.Tuesday, 17, 1.028260f),
        (DaysOfWeek.Tuesday, 18, 1.056527f),
        (DaysOfWeek.Tuesday, 19, 0.943519f),
        (DaysOfWeek.Tuesday, 20, 0.833978f),
        (DaysOfWeek.Tuesday, 21, 0.780885f),
        (DaysOfWeek.Tuesday, 22, 0.747342f),
        (DaysOfWeek.Tuesday, 23, 0.706323f),
        (DaysOfWeek.Wednesday, 0, 0.686860f),
        (DaysOfWeek.Wednesday, 1, 0.680879f),
        (DaysOfWeek.Wednesday, 2, 0.671005f),
        (DaysOfWeek.Wednesday, 3, 0.664748f),
        (DaysOfWeek.Wednesday, 4, 0.666187f),
        (DaysOfWeek.Wednesday, 5, 0.691133f),
        (DaysOfWeek.Wednesday, 6, 0.798381f),
        (DaysOfWeek.Wednesday, 7, 0.986397f),
        (DaysOfWeek.Wednesday, 8, 1.039476f),
        (DaysOfWeek.Wednesday, 9, 0.912936f),
        (DaysOfWeek.Wednesday, 10, 0.828463f),
        (DaysOfWeek.Wednesday, 11, 0.767509f),
        (DaysOfWeek.Wednesday, 12, 0.723043f),
        (DaysOfWeek.Wednesday, 13, 0.715072f),
        (DaysOfWeek.Wednesday, 14, 0.719140f),
        (DaysOfWeek.Wednesday, 15, 0.756836f),
        (DaysOfWeek.Wednesday, 16, 0.818701f),
        (DaysOfWeek.Wednesday, 17, 0.904933f),
        (DaysOfWeek.Wednesday, 18, 0.912132f),
        (DaysOfWeek.Wednesday, 19, 0.817568f),
        (DaysOfWeek.Wednesday, 20, 0.736159f),
        (DaysOfWeek.Wednesday, 21, 0.715324f),
        (DaysOfWeek.Wednesday, 22, 0.698255f),
        (DaysOfWeek.Wednesday, 23, 0.649769f),
        (DaysOfWeek.Thursday, 0, 0.615840f),
        (DaysOfWeek.Thursday, 1, 0.611668f),
        (DaysOfWeek.Thursday, 2, 0.597037f),
        (DaysOfWeek.Thursday, 3, 0.579310f),
        (DaysOfWeek.Thursday, 4, 0.560680f),
        (DaysOfWeek.Thursday, 5, 0.587819f),
        (DaysOfWeek.Thursday, 6, 0.613427f),
        (DaysOfWeek.Thursday, 7, 0.731383f),
        (DaysOfWeek.Thursday, 8, 0.730415f),
        (DaysOfWeek.Thursday, 9, 0.729493f),
        (DaysOfWeek.Thursday, 10, 0.707108f),
        (DaysOfWeek.Thursday, 11, 0.676434f),
        (DaysOfWeek.Thursday, 12, 0.646095f),
        (DaysOfWeek.Thursday, 13, 0.641394f),
        (DaysOfWeek.Thursday, 14, 0.653523f),
        (DaysOfWeek.Thursday, 15, 0.696669f),
        (DaysOfWeek.Thursday, 16, 0.766910f),
        (DaysOfWeek.Thursday, 17, 0.836310f),
        (DaysOfWeek.Thursday, 18, 0.877734f),
        (DaysOfWeek.Thursday, 19, 0.828079f),
        (DaysOfWeek.Thursday, 20, 0.766388f),
        (DaysOfWeek.Thursday, 21, 0.721290f),
        (DaysOfWeek.Thursday, 22, 0.692140f),
        (DaysOfWeek.Thursday, 23, 0.642686f),
        (DaysOfWeek.Friday, 0, 0.604272f),
        (DaysOfWeek.Friday, 1, 0.607754f),
        (DaysOfWeek.Friday, 2, 0.609189f),
        (DaysOfWeek.Friday, 3, 0.610987f),
        (DaysOfWeek.Friday, 4, 0.599694f),
        (DaysOfWeek.Friday, 5, 0.633689f),
        (DaysOfWeek.Friday, 6, 0.701935f),
        (DaysOfWeek.Friday, 7, 0.804372f),
        (DaysOfWeek.Friday, 8, 0.855806f),
        (DaysOfWeek.Friday, 9, 0.812653f),
        (DaysOfWeek.Friday, 10, 0.746612f),
        (DaysOfWeek.Friday, 11, 0.704187f),
        (DaysOfWeek.Friday, 12, 0.684745f),
        (DaysOfWeek.Friday, 13, 0.693636f),
        (DaysOfWeek.Friday, 14, 0.700557f),
        (DaysOfWeek.Friday, 15, 0.723909f),
        (DaysOfWeek.Friday, 16, 0.789766f),
        (DaysOfWeek.Friday, 17, 0.843642f),
        (DaysOfWeek.Friday, 18, 0.874343f),
        (DaysOfWeek.Friday, 19, 0.823000f),
        (DaysOfWeek.Friday, 20, 0.774093f),
        (DaysOfWeek.Friday, 21, 0.717158f),
        (DaysOfWeek.Friday, 22, 0.686906f),
        (DaysOfWeek.Friday, 23, 0.615362f),
        (DaysOfWeek.Saturday, 0, 0.618557f),
        (DaysOfWeek.Saturday, 1, 0.588649f),
        (DaysOfWeek.Saturday, 2, 0.570995f),
        (DaysOfWeek.Saturday, 3, 0.562068f),
        (DaysOfWeek.Saturday, 4, 0.557095f),
        (DaysOfWeek.Saturday, 5, 0.560831f),
        (DaysOfWeek.Saturday, 6, 0.553826f),
        (DaysOfWeek.Saturday, 7, 0.600460f),
        (DaysOfWeek.Saturday, 8, 0.641796f),
        (DaysOfWeek.Saturday, 9, 0.674696f),
        (DaysOfWeek.Saturday, 10, 0.669405f),
        (DaysOfWeek.Saturday, 11, 0.642892f),
        (DaysOfWeek.Saturday, 12, 0.608789f),
        (DaysOfWeek.Saturday, 13, 0.570126f),
        (DaysOfWeek.Saturday, 14, 0.577473f),
        (DaysOfWeek.Saturday, 15, 0.623242f),
        (DaysOfWeek.Saturday, 16, 0.730025f),
        (DaysOfWeek.Saturday, 17, 0.824675f),
        (DaysOfWeek.Saturday, 18, 0.874494f),
        (DaysOfWeek.Saturday, 19, 0.819868f),
        (DaysOfWeek.Saturday, 20, 0.733716f),
        (DaysOfWeek.Saturday, 21, 0.688087f),
        (DaysOfWeek.Saturday, 22, 0.664520f),
        (DaysOfWeek.Saturday, 23, 0.609859f),
        (DaysOfWeek.Sunday, 0, 0.660038f),
        (DaysOfWeek.Sunday, 1, 0.644887f),
        (DaysOfWeek.Sunday, 2, 0.605623f),
        (DaysOfWeek.Sunday, 3, 0.604854f),
        (DaysOfWeek.Sunday, 4, 0.596781f),
        (DaysOfWeek.Sunday, 5, 0.595746f),
        (DaysOfWeek.Sunday, 6, 0.582392f),
        (DaysOfWeek.Sunday, 7, 0.596523f),
        (DaysOfWeek.Sunday, 8, 0.623121f),
        (DaysOfWeek.Sunday, 9, 0.632372f),
        (DaysOfWeek.Sunday, 10, 0.623137f),
        (DaysOfWeek.Sunday, 11, 0.596488f),
        (DaysOfWeek.Sunday, 12, 0.574566f),
        (DaysOfWeek.Sunday, 13, 0.554209f),
        (DaysOfWeek.Sunday, 14, 0.561311f),
        (DaysOfWeek.Sunday, 15, 0.597632f),
        (DaysOfWeek.Sunday, 16, 0.681371f),
        (DaysOfWeek.Sunday, 17, 0.780751f),
        (DaysOfWeek.Sunday, 18, 0.791795f),
        (DaysOfWeek.Sunday, 19, 0.721423f),
        (DaysOfWeek.Sunday, 20, 0.658679f),
        (DaysOfWeek.Sunday, 21, 0.638100f),
        (DaysOfWeek.Sunday, 22, 0.609009f),
        (DaysOfWeek.Sunday, 23, 0.546243f));

    /// <summary>
    /// Gets the prices from the supplied hour.
    /// </summary>
    /// <param name="day">The day being queried.</param>
    /// <param name="hour">The hour being queried.</param>
    /// <returns>The price at supplied hour.</returns>
    public static float GetHourPrice(DaysOfWeek day, int hour)
    {
        if (!Enum.IsDefined(typeof(DaysOfWeek), day))
            throw new ArgumentOutOfRangeException(nameof(day), "Invalid day of week.");
        else if (hour < 0 || hour > 23)
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23.");

        return _energyPriceTable.First(x => x.Day == day && x.Hour == hour).Price;
    }

    /// <summary>
    /// Gets a dictionary of all prices for a day.
    /// </summary>
    /// <param name="day">The day being queried.</param>
    /// <returns>A Dictionary with (hour, price) tuples.</returns>
    public static Dictionary<int, float> GetDayPrice(DaysOfWeek day)
    {
        if (!Enum.IsDefined(typeof(DaysOfWeek), day))
            throw new ArgumentOutOfRangeException(nameof(day), "Invalid day of week.");

        return _energyPriceTable
            .Where(x => x.Day == day)
            .ToDictionary(x => x.Hour, x => (float)x.Price);
    }
}