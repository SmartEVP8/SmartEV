namespace Core.Charging;

using Core.Charging.ChargingModel;

/// <summary>
/// Charger that supports one vehicle at a time.
/// </summary>
/// <param name="id">The id of the charger.</param>
/// <param name="maxPowerKW">The maximum power output in kilowatts.</param>
/// <param name="connectors">The connector set available at this charger.</param>
public sealed class SingleCharger(int id, int maxPowerKW, Connectors connectors)
    : ChargerBase(id, maxPowerKW)
{
    private Connector _connector = connectors.AttachedConnectors.Left;

    /// <summary>
    /// Returns the power output for the given SoC, capped by the charger's max power.
    /// </summary>
    /// <param name="maxKW">The maximum power available at the charger in kilowatts.</param>
    /// <param name="soc">The current state of charge of the vehicle (0.0 to 1.0).</param>
    /// <returns>The power delivered to the vehicle in kilowatts.</returns>
    public double GetPowerOutput(double maxKW, double soc)
        => maxKW * ChargingCurve.PowerFraction(soc);

    /// <inheritdoc/>
    public override bool CanConnect() => _connector.IsFree;

    /// <summary>
    /// Attempts to connect a vehicle. Returns false if already occupied.
    /// </summary>
    /// <returns>True if the vehicle was successfully connected; otherwise false.</returns>
    public bool TryConnect()
    {
        if (!_connector.IsFree) return false;
        _connector.Activate();
        return true;
    }

    /// <summary>Disconnects the currently connected vehicle.</summary>
    public void Disconnect() => _connector.Deactivate();
}
