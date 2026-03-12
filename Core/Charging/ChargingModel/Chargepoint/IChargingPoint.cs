namespace Core.Charging.ChargingModel.Chargepoint;

using System.Collections.Immutable;
using Core.Shared;

public interface IChargingPoint
{
    /// <summary>
    /// Get a list of the compatible sockets for this charging point, no duplicates.
    /// </summary>
    /// <returns>Get a list of the compatible sockets for the charging point, no duplicates.</returns>
    ImmutableArray<Socket> GetSockets();


    (double PowerA, double PowerB) GetPowerDistribution(double maxKW, double socA, double? socB, IChargingCurve curveA, IChargingCurve? curveB);
}
