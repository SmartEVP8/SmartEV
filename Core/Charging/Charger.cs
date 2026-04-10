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

}
