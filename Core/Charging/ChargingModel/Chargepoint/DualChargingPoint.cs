namespace Core.Charging.ChargingModel.Chargepoint;

using System.Collections.Immutable;
using Core.Shared;

/// <summary>
/// A charging point with two identical connector sets, allowing two vehicles to charge
/// simultaneously. Both sides support the same socket types — the right side is always
/// a copy of the left side's connector configuration.
/// Power not consumed by one side (due to charging curve taper or car rate limit) is
/// redistributed to the other, up to each connector's physical rated limit.
/// </summary>
public class DualChargingPoint(Connectors connectors) : IDualChargingPoint
{
    private readonly ImmutableArray<Socket> _sockets = [.. connectors.Sockets];

    private Connectors _leftSide = connectors.Copy();
    private Connectors _rightSide = connectors.Copy();

    /// <inheritdoc/>
    public ImmutableArray<Socket> GetSockets() => _sockets;

    /// <inheritdoc/>
    public (double PowerA, double PowerB) GetPowerDistribution(
        double maxKW,
        double socA,
        double socB,
        double maxChargeRateKWA,
        double maxChargeRateKWB)
    {
        var nominal = maxKW / 2.0;

        var fractionA = ChargingCurve.PowerFraction(socA);
        var fractionB = ChargingCurve.PowerFraction(socB);

        // Physical cap = min(connector rating, car's own onboard charger limit)
        var physicalCapA = Math.Min(_leftSide.ActivePowerKW, maxChargeRateKWA);
        var physicalCapB = Math.Min(_rightSide.ActivePowerKW, maxChargeRateKWB);

        var ceilA = Math.Min(nominal, physicalCapA) * fractionA;
        var ceilB = Math.Min(nominal, physicalCapB) * fractionB;

        var surplusA = nominal - ceilA;
        var surplusB = nominal - ceilB;

        var finalA = Math.Min(ceilA + Math.Max(0, surplusB), physicalCapA);
        var finalB = Math.Min(ceilB + Math.Max(0, surplusA), physicalCapB);

        return (finalA, finalB);
    }

    /// <inheritdoc/>
    public ChargingSide? CanConnect(Socket socket)
    {
        if (_leftSide.IsFree && _leftSide.Supports(socket)) return ChargingSide.Left;
        if (_rightSide.IsFree && _rightSide.Supports(socket)) return ChargingSide.Right;
        return null;
    }

    /// <inheritdoc/>
    public ChargingSide? TryConnect(Socket socket)
    {
        if (TryActivate(ref _leftSide, socket)) return ChargingSide.Left;
        if (TryActivate(ref _rightSide, socket)) return ChargingSide.Right;
        return null;
    }

    /// <inheritdoc/>
    public void Disconnect(ChargingSide side)
    {
        if (side == ChargingSide.Left) _leftSide.Deactivate();
        else _rightSide.Deactivate();
    }

    private static bool TryActivate(ref Connectors connectors, Socket socket)
    {
        if (!connectors.IsFree || !connectors.Supports(socket)) return false;
        connectors.Activate(connectors.GetConnectorFor(socket));
        return true;
    }
}