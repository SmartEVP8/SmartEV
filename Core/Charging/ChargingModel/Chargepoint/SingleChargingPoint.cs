namespace Core.Charging.ChargingModel.Chargepoint;

using System.Collections.Immutable;
using Core.Shared;

/// <summary>
/// SingleChargingPoint represents a charging point with a single connector, allowing for one electric vehicle to charge at a time.
/// </summary>
public readonly struct SingleChargingPoint(Connectors connectors) : IChargingPoint
{
    private readonly Connectors _connectors = connectors;
    private readonly ImmutableArray<Socket> _sockets =
              [.. connectors.Sockets];

    /// <inheritdoc/>
    public ImmutableArray<Socket> GetSockets() => _sockets;

    /// <inheritdoc/>
    public (double PowerA, double PowerB) GetPowerDistribution(
    double maxKW,
    double socA,
    double? socB,
    IChargingCurve curveA,
    IChargingCurve? curveB)
    {
        if (socB is not null)
            throw new InvalidOperationException("SingleChargingPoint cannot have a second car.");

        var cap = Math.Min(maxKW, _connectors.ActivePowerKW);
        return (cap * curveA.PowerFraction(socA), 0.0);
    }
}
