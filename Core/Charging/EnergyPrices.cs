namespace Core.Charging;

using System.Collections.Immutable;
using System;
using System.Globalization;
using Core.Helper;

/// <summary>
/// Provides estimated EV charging prices in DKK/kWh for each hour of the day (0–23).
/// </summary>
/// <remarks>
/// based on an interpolation between day-ahead spot prices from energidataservice.dk <see href="https://energidataservice.dk/tso-electricity/DayAheadPrices"/>
/// and elbiil.dk <see href="https://www.elbiil.dk/opladning/opladning-paa-farten"/>.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="EnergyPrices"/> class.
/// Initializes the energy price table by reading the csv file and coverting to (Day, Hour, Price).
/// </remarks>
/// <param name="csvPath">The path to the csv containing the pricing data.</param>
/// <param name="random">A random number generator for simulating price fluctuations.</param>
public class EnergyPrices(FileInfo csvPath, Random random)
{
    private readonly Random _random = random;

    /// <summary>
    /// Array of energy price for each hour.
    /// </summary>
    private readonly ImmutableArray<(DayOfWeek Day, int Hour, float Price)> _energyPriceTable = [.. File.ReadAllLines(csvPath.ToString())
            .Skip(1)
            .Select(line => line.Split(','))
            .Select(parts => (
                Day: Enum.Parse<DayOfWeek>(parts[0]),
                Hour: int.Parse(parts[1]),
                Price: float.Parse(parts[2], CultureInfo.InvariantCulture)))];

    /// <summary>
    /// Gets the prices from the supplied hour.
    /// </summary>
    /// <param name="day">The day being queried.</param>
    /// <param name="hour">The hour being queried.</param>
    /// <returns>The integer price at a given hour.</returns>
    public float GetHourPrice(DayOfWeek day, int hour)
    {
        if (!Enum.IsDefined(day))
            throw Log.Error(0, 0, new ArgumentOutOfRangeException(nameof(day), "Invalid day of week."), ("Day", day), ("Hour", hour));
        else if (hour < 0 || hour > 23)
            throw Log.Error(0, 0, new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23."), ("Day", day), ("Hour", hour));

        return _energyPriceTable.First(x => x.Day == day && x.Hour == hour).Price;
    }

    /// <summary>
    /// Calculates a price and applies a random deviation of 0–20% to the base hourly price.
    /// Call this periodically to simulate dynamic pricing.
    /// </summary>
    /// <param name="day">The day being queried.</param>
    /// <param name="hour">The hour being queried.</param>
    /// <returns>The price at the specified day and hour ± deviation.</returns>
    public float CalculatePrice(DayOfWeek day, int hour)
    {
        var basePrice = GetHourPrice(day, hour);
        var deviation = _random.NextSingle() * 0.20f; // 0–20%
        var sign = _random.Next(2) == 0 ? 1.0f : -1.0f;
        return basePrice * (1.0f + (sign * deviation));
    }
}
