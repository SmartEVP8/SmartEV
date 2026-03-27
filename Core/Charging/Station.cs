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
/// <param name="energyPrices">EnergyPrices based on time of day.</param>
public class Station(ushort id,
                string name,
                string address,
                Position position,
                List<ChargerBase> chargers,
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

    /// <summary>
    /// Gets the total number of reservation requests ever made to this station.
    /// </summary>
    public ushort TotalReservations { get; private set; }

    /// <summary>
    /// Gets the total number of cancellation requests ever made to this station.
    /// </summary>
    public ushort TotalCancellations { get; private set; }

    /// <summary>
    /// Gets the current price of energy at the station.
    /// </summary>
    public float Price { get; private set; }

    private (DayOfWeek Day, int Hour) _lastPriceUpdate = ((DayOfWeek)(-1), -1);

    /// <summary>
    /// Gets the list of chargers on a station.
    /// </summary>
    public IReadOnlyList<ChargerBase> Chargers => _chargers;

    private readonly List<ChargerBase> _chargers = chargers;

    /// <summary>
    /// Updates and caches the price for the given day and hour. If changed
    /// since the last update, recalculates; otherwise uses the cached price.
    /// </summary>
    /// <param name="day">The day of the week.</param>
    /// <param name="hour">The hour of the day (0–23).</param>
    /// <returns>The current price at the station.</returns>
    public float UpdatePrice(DayOfWeek day, int hour)
    {
        if ((day, hour) != _lastPriceUpdate)
        {
            _lastPriceUpdate = (day, hour);
            Price = energyPrices.CalculatePrice(day, hour);
        }

        return Price;
    }

    /// <summary>
    /// Collects the reservation and cancellation counts since the last snapshot, then resets both counters.
    /// </summary>
    /// <returns> Returns the reservation and cancellation counts since the last snapshot.</returns>
    public (ushort reservations, ushort cancellations) CountReservationsCancellations()
    {
        var reservationsAndCancellations = (TotalReservations, TotalCancellations);
        TotalReservations = 0;
        TotalCancellations = 0;
        return reservationsAndCancellations;
    }
    
    /// <summary>
    /// Increments the amount of reservations on a station, and updates the total amount of reservations.
    /// </summary>
    public void IncrementReservations() => TotalReservations++;

    /// <summary>
    /// Decrements the amount of reservations on a station, and updates the total amount of cancellations.
    /// </summary>
    public void IncrementCancellations() => TotalCancellations++;
}
