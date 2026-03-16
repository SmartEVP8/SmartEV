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
    private readonly int _id = id;

    private readonly int _maxPowerKW = maxPowerKW;

    private readonly IChargingPoint _chargingPoint = chargingPoint;

    /// <summary>A queue of EVs waiting to charge at this charger.</summary>
    /// <remarks>Points to the index of the EV in the list of EVs.</remarks>
    private readonly Queue<int> _queue = new();

    /// <summary>Gets the unique identifier for this charger.</summary>
    public int Id => _id;

    /// <summary>Gets the maximum power output of this charger.</summary>
    public int MaxPowerKW => _maxPowerKW;

    /// <summary>
    /// Gets the charging point associated with this charger, which represents the physical location(s) where electric vehicles can be connected for charging.
    /// </summary>
    public IChargingPoint ChargingPoint => _chargingPoint;

    /// <summary>Gets the queue of EVs waiting at the charger.</summary>
    public Queue<int> Queue => _queue;
}
