namespace Core.Charging;

/// <summary>
/// A Charger represents a single charging unit within a charging station.
/// Each charger has a unique identifier, a maximum power output in kilowatts (kW), and a list of charging points that it can support.
/// The charger is responsible for delivering electricity to the connected electric vehicles through its charging points, and the maximum power output determines how quickly the vehicles can be charged.
/// </summary>
/// <param name="id">Unique identifer for the charger.</param>
/// <param name="maxPowerKW">The maximum power that a Charger can distribute between <paramref name="chargingPoint"/>.</param>
/// <param name="chargingPoint">The charging point represents one or two physical locations where an electric vehicle can be connected.</param>
public readonly struct Charger(int id, int maxPowerKW, IChargingPoint chargingPoint)
{
    private int Id { get; } = id;

    private int MaxPowerKW { get; } = maxPowerKW;

    private IChargingPoint ChargingPoint { get; } = chargingPoint;

    /// <summary>A queue of EVs waiting to charge at this charger.</summary>
    /// <remarks>Points to the index of the EV in the list of EVs.</remarks>
    private Queue<int> Queue { get; } = new();
}
