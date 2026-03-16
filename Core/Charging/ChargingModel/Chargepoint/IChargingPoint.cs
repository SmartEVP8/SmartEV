namespace Core.Charging.ChargingModel.Chargepoint;

using System.Collections.Immutable;
using Core.Shared;

/// <summary>
/// Marker interface for all charging point types.
/// Use <see cref="ISingleChargingPoint"/> or <see cref="IDualChargingPoint"/>
/// </summary>
public interface IChargingPoint
{
    /// <summary>
    /// Returns the set of sockets supported by the charging point.
    /// </summary>
    /// <returns>
    /// An immutable array containing the sockets supported by the charging point.
    /// </returns>
    ImmutableArray<Socket> GetSockets();
}