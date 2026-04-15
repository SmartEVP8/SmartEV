namespace Core.Charging;

using Core.Shared;
using System;

/// <summary>
/// An EV charging station.
/// </summary>
/// <param name="id">The id of the station.</param>
/// <param name="address">The physical address of the station, e.g. 'Logistikparken 12'.</param>
/// <param name="position">Longitude/Latitude of the station.</param>
/// <param name="chargers">A list of the chargers attached to the station.</param>
/// <param name="energyPrices">EnergyPrices based on time of day.</param>
public class Station(ushort id,
                string address,
                Position position,
                List<ChargerBase> chargers,
                EnergyPrices energyPrices)
{
    /// <summary>
    /// Gets the position of the station.
    /// </summary>
    public Position Position { get; private set; } = position;

    /// <summary>
    /// Gets the id of the station.
    /// </summary>
    public ushort Id => id;

    /// <summary>
    /// Gets the address of the station.
    /// </summary>
    public string Address => address;

    /// <summary>
    /// The stations reservations.
    /// </summary>
    public readonly Reservations Reservations = new();

    /// <summary>
    /// Gets the current price of energy at this station based on the time of day.
    /// </summary>
    /// <param name="time">The current time.</param>
    /// <returns>The price at the current day and hour.</returns>
    public float GetPrice(Time time)
    {
        var key = (Day: time.DayOfWeek, Hour: (int)time.Hours);

        if (key != _lastPriceUpdate)
        {
            _lastPriceUpdate = key;
            _price = energyPrices.CalculatePrice(key.Day, key.Hour);
        }

        return _price;
    }

    /// <summary>
    /// Sets the position of the station.
    /// </summary>
    /// <param name="newPosition">The new position for the station.</param>
    public void SetPosition(Position newPosition) => Position = newPosition;

    /// <summary>
    /// Gets the list of chargers on a station.
    /// </summary>
    public IReadOnlyList<ChargerBase> Chargers => _chargers;

    private readonly List<ChargerBase> _chargers = chargers;

    private (DayOfWeek Day, int Hour) _lastPriceUpdate = ((DayOfWeek)(-1), -1);

    private float _price;
}

public record Reservation(int EVId, Time TimeOfArrival, double SoCAtArrival, double TargetSoC) : IComparable<Reservation>
{
    /// <inheritdoc/>
    public int CompareTo(Reservation? other)
    {
        if (other is null) return 1;
        if (TimeOfArrival < other.TimeOfArrival) return -1;
        if (TimeOfArrival > other.TimeOfArrival) return 1;
        return EVId.CompareTo(other.EVId);
    }
}

/// <summary>
/// Manages a stations reservations.
/// </summary>
public class Reservations()
{
    private readonly SortedSet<Reservation> _reservations = [];
    private readonly Dictionary<int, Reservation> _index = [];
    private uint _totalReservationsInPeriod;
    private uint _totalCancellationsInPeriod;


    /// <summary>
    /// Gets all the ev ids that have a reservation on the station.
    /// </summary>
    public IReadOnlyList<int> GetEVsOnRoute => [.. _index.Keys];

    /// <summary>
    /// Returns reservation and cancellation counts since the last snapshot, then resets both counters.
    /// </summary>
    /// <returns>A tuple with the number of reservations and cancellations that the stations has had since last call.</returns>
    public (uint Reservations, uint Cancellations) SnapshotAndResetCounters()
    {
        var snapshot = (_totalReservationsInPeriod, _totalCancellationsInPeriod);
        _totalReservationsInPeriod = 0;
        _totalCancellationsInPeriod = 0;
        return snapshot;
    }

    /// <summary>
    /// Gets the next reservation by time of arrival.
    /// </summary>
    /// <param name="res">The next reservation, or <see langword="null"/> if no reservations exist.</param>
    /// <returns><see langword="true"/> if a reservation was found, <see langword="false"/> otherwise.</returns>
    public bool TryGetNext(out Reservation? res)
    {
        res = _reservations.Min;
        return res is not null;
    }

    /// <summary>
    /// Adds a reservation to the station and increments the reservation counter.
    /// </summary>
    /// <param name="reservation">The reservation to add.</param>
    public void Reserve(Reservation reservation)
    {
        _reservations.Add(reservation);
        _index[reservation.EVId] = reservation;
        _totalReservationsInPeriod++;
    }

    /// <summary>
    /// Cancels the reservation associated with the given EV id, removing it from the station and incrementing the cancellation counter.
    /// Does nothing if no reservation exists for the given id.
    /// </summary>
    /// <param name="evId">The id of the EV whose reservation should be cancelled.</param>
    public void Cancel(int evId)
    {
        if (_index.TryGetValue(evId, out var reservation))
        {
            _reservations.Remove(reservation);
            _index.Remove(evId);
            _totalCancellationsInPeriod++;
        }
    }
}
