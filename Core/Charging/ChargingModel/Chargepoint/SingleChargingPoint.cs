namespace Core.Charging.ChargingModel.Chargepoint;

/// <summary>
/// A charging point with a single connector; supports one vehicle at a time.
/// </summary>
public class SingleChargingPoint(Connectors connectors) : ISingleChargingPoint
{
    private Connector _connector = connectors.AttachedConnectors.Left;

    /// <inheritdoc/>
    public double GetPowerOutput(double maxKW, double soc) => maxKW * ChargingCurve.PowerFraction(soc);

    /// <inheritdoc/>
    public bool CanConnect() => _connector.IsFree;

    /// <inheritdoc/>
    public bool TryConnect()
    {
        if (!_connector.IsFree)
            return false;

        _connector.Activate();
        return true;
    }

    /// <inheritdoc/>
    public void Disconnect() => _connector.Deactivate();
}
