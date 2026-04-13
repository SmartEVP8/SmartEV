namespace Core.Charging;

/// <summary>
/// Base class for chargers. Holds identity, power cap, and the queue of waiting EVs.
/// </summary>
/// <param name="id">The id of the charger.</param>
/// <param name="maxPowerKW">The maximum power output in kilowatts.</param>
public abstract class ChargerBase(int id, int maxPowerKW)
{
    /// <summary>Gets the charger identifier.</summary>
    public int Id { get; } = id;

    /// <summary>Gets the maximum power output in kilowatts.</summary>
    public int MaxPowerKW { get; } = maxPowerKW;

    /// <summary>Gets the queue of EV ids waiting to charge at this charger.</summary>
    public Queue<int> Queue { get; } = new();

    /// <summary>Returns whether a vehicle can connect to this charger right now.</summary>
    /// <returns>True if a connector is free; otherwise false.</returns>
    public abstract bool CanConnect();
}
