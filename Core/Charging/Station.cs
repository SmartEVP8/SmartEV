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
/// <param name="price">The KWh price of charging at the station.</param>
/// <param name="random">An injected instance of Math.Random.</param>
public class Station(ushort id,
                string name,
                string address,
                Position position,
                List<Charger>? chargers,
                float price,
                Random random)
{

    /// <summary>The current KWh price at the station.</summary>
    public float Price = price;
    private readonly Random _random = random;
    private readonly ushort id = id;
    private readonly string _name = name;
    private readonly string _address = address;
    public readonly Position Position = position;
    private readonly List<Charger>? _chargers = chargers;

    /// <summary>
    /// Calculates the price of a specific station.
    /// </summary>
    /// <param name="hour">The hour being queried.</param>
    /// <remarks>
    /// The new price is randomly generated in the range [3.0, 5.0].
    /// Call this periodically to simulate dynamic pricing.
    /// </remarks>
    public void CalculatePrice(DayOfWeek day = DayOfWeek.Monday, int hour = 12)
    {
        var basePrice = EnergyPrices.GetHourPrice(day, hour);
        var deviation = 0.10f + (_random.NextSingle() * 0.10f); // 10–20%
        var sign = _random.Next(2) == 0 ? 1.0f : -1.0f;
        Price = basePrice * (1.0f + (sign * deviation));
    }

    public uint GetId() => _id;
}
