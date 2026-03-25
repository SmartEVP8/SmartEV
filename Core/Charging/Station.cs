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
    /// <summary>
    /// Gets the position of the station.
    /// </summary>
    public Position Position => position;

    /// <summary>
    /// Gets the id of the station.
    /// </summary>
    public ushort Id => id;

    /// <summary>
    /// Gets the name of the station.
    /// </summary>
    public string Name => name;

    /// <summary>
    /// Gets the address of the station.
    /// </summary>
    public string Address => address;

    private ushort _totalReservations = 0;
    private ushort _totalCancellations = 0;
    private ushort _currentAmountOfReservations = 0;

    /// <summary>
    /// Gets the total number of reservation requests ever made to this station.
    /// </summary>
    public uint TotalReservations => _totalReservations;

    /// <summary>
    /// Gets the total number of cancellation requests ever made to this station.
    /// </summary>
    public uint TotalCancellations => _totalCancellations;

    /// <summary>
    /// Gets the number of active reservations at this station.
    /// </summary>
    public ushort CurrentAmountOfReservations => _currentAmountOfReservations;

    /// <summary>
    /// Gets the list of chargers on a station.
    /// </summary>
    public IReadOnlyList<ChargerBase> Chargers => _chargers;

    private readonly List<ChargerBase> _chargers = chargers;

    /// <summary>
    /// Calculates the price of a specific station.
    /// </summary>
    /// <param name="day">The day being queried.</param>
    /// <param name="hour">The hour being queried.</param>
    /// <remarks>
    /// The new price is randomly generated in the range [3.0, 5.0].
    /// Call this periodically to simulate dynamic pricing.
    /// </remarks>
    /// <returns>The calculated price.</returns>
    public float CalculatePrice(DayOfWeek day, int hour)
    {
        var basePrice = energyPrices.GetHourPrice(day, hour);
        var deviation = 0.10f + (random.NextSingle() * 0.10f); // 10–20%
        var sign = random.Next(2) == 0 ? 1.0f : -1.0f;
        return basePrice * (1.0f + (sign * deviation));
    }

    /// <summary>
    /// Collects the reservation and cancellation counts since the last snapshot, then resets both counters.
    /// </summary>
    /// <returns> Returns the reservation and cancellation counts since the last snapshot.</returns>
    public (ushort reservations, ushort cancellations) CountReservationsCancellations()
    {
        var reservationsAndCancellations = (_totalReservations, _totalCancellations);
        _totalReservations = 0;
        _totalCancellations = 0;
        return reservationsAndCancellations;
    }

    /// <summary>
    /// Increments the amount of reservations on a station, and updates the total amount of reservations.
    /// </summary>
    public void IncrementReservations()
    {
        _currentAmountOfReservations++;
        _totalReservations++;
    }
    
    /// <summary>
    /// Decrements the amount of reservations on a station, and updates the total amount of cancellations.
    /// </summary>
    public void DecrementReservations()
    {
        _currentAmountOfReservations = (ushort)Math.Max(0, _currentAmountOfReservations - 1);
        _totalCancellations++;
    }
}
