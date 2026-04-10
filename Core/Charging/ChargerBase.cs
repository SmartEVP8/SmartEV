namespace Core.Charging;

/// <summary>
/// Base class for chargers.
/// </summary>
/// <param name="id">The id of the charger.</param>
/// <param name="maxPowerKW">The maximum power output in kilowatts.</param>
public abstract class ChargerBase(int id, int maxPowerKW)
{
    /// <summary>
    /// Gets the charger identifier.
    /// </summary>
    public int Id { get; } = id;

    /// <summary>
    /// Gets the maximum power output in kilowatts.
    /// </summary>
    public int MaxPowerKW { get; } = maxPowerKW;

    /// <summary>Gets a queue of EVs waiting to charge at this charger.</summary>
    /// <remarks>Points to the index of the EV in the list of EVs.</remarks>
    public Queue<int> Queue { get; } = new();
}
