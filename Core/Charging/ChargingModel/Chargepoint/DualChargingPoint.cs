namespace Core.Charging.ChargingModel.Chargepoint;

using System.Collections.Immutable;
using Core.Shared;

/// <summary>
/// DualChargingPoint represents a charging point with two connectors, allowing for two electric vehicles to charge simultaneously. Each connector can be of a different type, providing flexibility for various vehicle models and charging standards. The DualChargingPoint is designed to optimize the use of charging infrastructure by enabling multiple vehicles to charge at the same time, which can be particularly beneficial in high-traffic areas or for stations with limited space.
/// </summary>
public class DualChargingPoint(Connectors leftSide, Connectors rightSide) : IChargingPoint
{
    private readonly ImmutableArray<Socket> _sockets = [.. leftSide.Sockets, .. rightSide.Sockets];
    private Connectors _leftSide = leftSide;
    private Connectors _rightSide = rightSide;

    public ImmutableArray<Socket> GetSockets() => _sockets;

    public (double PowerA, double PowerB) GetPowerDistribution(
            double maxKW,
            double socA,
            double? socB,
            IChargingCurve curveA,
            IChargingCurve? curveB)
    {
        double leftMax = _leftSide.ActivePowerKW;
        double rightMax = _rightSide.ActivePowerKW;

        if (socB is null)
        {
            var cap = Math.Min(maxKW, leftMax);
            return (cap * curveA.PowerFraction(socA), 0.0);
        }
        var nominal = maxKW / 2.0;
        var ceilA = Math.Min(nominal, leftMax) * curveA.PowerFraction(socA);
        var ceilB = Math.Min(nominal, rightMax) * curveB!.PowerFraction(socB.Value);
        var surplusA = nominal - ceilA;
        var surplusB = nominal - ceilB;

        var finalA = Math.Min(ceilA + Math.Max(0, surplusB), leftMax * curveA.PowerFraction(socA));
        var finalB = Math.Min(ceilB + Math.Max(0, surplusA), rightMax * curveB!.PowerFraction(socB.Value));

        return (finalA, finalB);
    }

    public bool CanConnect(Socket socket, out bool isLeft)
    {
        if (_leftSide.IsFree && _leftSide.Supports(socket))
        {
            isLeft = true;
            return true;
        }
        if (_rightSide.IsFree && _rightSide.Supports(socket))
        {
            isLeft = false;
            return true;
        }
        isLeft = false;
        return false;
    }

    public bool TryConnect(Socket socket, out bool isLeft)
    {
        if (_leftSide.IsFree && _leftSide.Supports(socket))
        {
            var connector = _leftSide.GetConnectorFor(socket);
            _leftSide.Activate(connector);
            isLeft = true;
            return true;
        }
        if (_rightSide.IsFree && _rightSide.Supports(socket))
        {
            var connector = _rightSide.GetConnectorFor(socket);
            _rightSide.Activate(connector);
            isLeft = false;
            return true;
        }
        isLeft = false;
        return false;
    }

    public void Disconnect(bool isLeft)
    {
        if (isLeft) _leftSide.Deactivate();
        else _rightSide.Deactivate();
    }
}


