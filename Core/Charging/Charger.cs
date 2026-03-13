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
    private readonly IChargingPoint _chargingpoint = chargingPoint;

    /// <summary>
    /// Exposes selected charger fields as read-only properties.
    /// </summary>
    /// <remarks>
    /// These properties provide controlled access to the charger’s identifier,
    /// maximum power, and charging point so that other components and unit tests
    /// can inspect charger configuration without relying on reflection or direct
    /// access to private fields.
    /// </remarks>
    public int Id => _id;
    public int MaxPowerKW => _maxPowerKW;
    public IChargingPoint ChargingPoint => _chargingpoint;
}
