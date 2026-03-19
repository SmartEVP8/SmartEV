namespace Core.Charging;

using System.Collections.Immutable;
using System;
using System.Globalization;

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
public class EnergyPrices(FileInfo csvPath)
{
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
    public Dictionary<int, float> GetDayPrice(DayOfWeek day)
    {
        if (!Enum.IsDefined(day))
            throw new ArgumentOutOfRangeException(nameof(day), "Invalid day of week.");

        return _energyPriceTable
            .Where(x => x.Day == day)
            .ToDictionary(x => x.Hour, x => x.Price);
    }
}
