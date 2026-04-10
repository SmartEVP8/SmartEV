namespace Core.Charging.ChargingModel.Chargepoint;

/// <summary>
/// A charging point with two identical connector sets, allowing two vehicles to charge
/// simultaneously.
/// Power not consumed by one side (due to charging curve taper or car rate limit) is
/// redistributed to the other, up to each connector's physical rated limit.
/// </summary>
public class DualChargingPoint(Connectors connectors) : IDualChargingPoint
{
    private Connector _leftSide = connectors.AttachedConnectors.Left;
    private Connector _rightSide = connectors.AttachedConnectors.Right;

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
        var physicalCapA = Math.Min(_leftSide.PowerKW, maxChargeRateKWA);
        var physicalCapB = Math.Min(_rightSide.PowerKW, maxChargeRateKWB);

        var ceilA = Math.Min(nominal, physicalCapA) * fractionA;
        var ceilB = Math.Min(nominal, physicalCapB) * fractionB;

        var surplusA = nominal - ceilA;
        var surplusB = nominal - ceilB;

        var finalA = Math.Min(ceilA + Math.Max(0, surplusB), physicalCapA);
        var finalB = Math.Min(ceilB + Math.Max(0, surplusA), physicalCapB);

        return (finalA, finalB);
    }

    /// <inheritdoc/>
    public ChargingSide? CanConnect()
    {
        if (_leftSide.IsFree) return ChargingSide.Left;
        if (_rightSide.IsFree) return ChargingSide.Right;
        return null;
    }

    /// <inheritdoc/>
    public ChargingSide? TryConnect()
    {
        if (TryActivate(ref _leftSide)) return ChargingSide.Left;
        if (TryActivate(ref _rightSide)) return ChargingSide.Right;
        return null;
    }

    /// <inheritdoc/>
    public void Disconnect(ChargingSide side)
    {
        if (side == ChargingSide.Left) _leftSide.Deactivate();
        else _rightSide.Deactivate();
    }

    private static bool TryActivate(ref Connector connector)
    {
        if (!connector.IsFree) return false;
        connector.Activate();
        return true;
    }
}
