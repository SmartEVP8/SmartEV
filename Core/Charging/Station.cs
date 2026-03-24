namespace Core.Charging;

using Core.Shared;
using System;

/// <summary>
/// An EV charging station.
/// </summary>
/// <param name="id">The id of the station.</param>
/// <param name="name">The name of the station, e.g. 'OK Aarselv, Logistikparken'.</param>
/// <param name="address">The physical address of the station, e.g. 'Logistikparken 12'.</param>
/// <param name="position">Longitude/Latitude of the station.</param>
/// <param name="chargers">A list of the chargers attached to the station.</param>
/// <param name="random">An injected instance of Math.Random.</param>
/// <param name="energyPrices">EnergyPrices based on time of day.</param>
public class Station(ushort id,
                string name,
                string address,
                Position position,
                List<ChargerBase> chargers,
                Random random,
                EnergyPrices energyPrices)
{
    /// <summary>Gets the position of the station.</summary>
    public Position Position => position;

    /// <summary>Gets the id of the station.</summary>
    public ushort Id => id;

    /// <summary>Gets the name of the station.</summary>
    public string Name => name;

    /// <summary>Gets the address of the station.</summary>
    public string Address => address;

    /// <summary>Gets the current price of energy at the station.</summary>
    public float Price { get; private set; }

    /// <summary>Gets the list of chargers on a station.</summary>
    public IReadOnlyList<ChargerBase> Chargers => _chargers;

    private readonly List<ChargerBase> _chargers = chargers;

    /// <summary>
    /// Calculates and sets the price of a specific station.
    /// </summary>
    /// <param name="day">The day being queried.</param>
    /// <param name="hour">The hour being queried.</param>
    /// <remarks>
    /// The new price is randomly generated in the range [3.0, 5.0].
    /// Call this periodically to simulate dynamic pricing.
    /// </remarks>
    public void CalculatePrice(DayOfWeek day, int hour)
    {
        var basePrice = energyPrices.GetHourPrice(day, hour);
        var deviation = 0.10f + (random.NextSingle() * 0.10f); // 10–20%
        var sign = random.Next(2) == 0 ? 1.0f : -1.0f;
        Price = basePrice * (1.0f + (sign * deviation));
    }
}
