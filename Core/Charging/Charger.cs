namespace Core.Charging;

using Core.Charging.ChargingModel.Chargepoint;
using Core.Shared;
using System.Collections.Immutable;

/// <summary>
/// Charger that can support charging one vehicle at a time.
/// </summary>
/// <param name="id">The id of the charger.</param>
/// <param name="maxPowerKW">The maximum power output in kilowatts.</param>
/// <param name="chargingPoint">The charging point instance.</param>
public sealed class SingleCharger(int id, int maxPowerKW, ISingleChargingPoint chargingPoint)
    : ChargerBase(id, maxPowerKW)
{
    /// <summary>
    /// Gets the charging point.
    /// </summary>
    public ISingleChargingPoint ChargingPoint { get; } = chargingPoint;

    /// <summary>
    /// Gets the sockets available at the given charger.
    /// </summary>
    /// <returns> An immutable array of sockets available at the charger. </returns>
    public override ImmutableArray<Socket> GetSockets() => ChargingPoint.GetSockets();

    /// <summary>Gets a queue of EVs waiting to charge at this charger.</summary>
    /// <remarks>Points to the index of the EV in the list of EVs.</remarks>
    public Queue<int> Queue { get; } = new();
}

/// <summary>
/// Charger than can support charging one or two EV's simultaneously.
/// </summary>
/// <param name="id">The id of the charger.</param>
/// <param name="maxPowerKW">The maximum power output in kilowatts.</param>
/// <param name="chargingPoint">The charging point instance.</param>
public sealed class DualCharger(int id, int maxPowerKW, IDualChargingPoint chargingPoint)
    : ChargerBase(id, maxPowerKW)
{
    /// <summary>
    /// Gets the charging point.
    /// </summary>
    public IDualChargingPoint ChargingPoint { get; } = chargingPoint;

    /// <summary>
    /// Gets the sockets available at the given charger.
    /// </summary>
    /// <returns> An immutable array of sockets available at the charger. </returns>
    public override ImmutableArray<Socket> GetSockets() => ChargingPoint.GetSockets();

    /// <summary>Gets a queue of EVs waiting to charge at this charger.</summary>
    /// <remarks>Points to the index of the EV in the list of EVs.</remarks>
    public Queue<int> Queue { get; } = new();
}