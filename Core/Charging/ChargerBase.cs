namespace Core.Charging;

using System.Collections.Immutable;
using Core.Shared;

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

    /// <summary>
    ///  Gets the sockets available at the given charger.
    /// </summary>
    /// <returns> An immutable array of sockets available at the charger. </returns>
    public abstract ImmutableArray<Socket> GetSockets();
}