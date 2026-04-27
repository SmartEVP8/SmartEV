namespace Core.Charging;

using Core.Charging.ChargingModel;
using Core.Shared;

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

    /// <summary>
    /// Gets or sets the active charging session at side A, or null if free. Always used for single chargers.
    /// </summary>
    public ActiveSession? Session { get; set; }

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

    /// <inheritdoc/>
    public override bool IsFree => Session is null;

    /// <inheritdoc/>
    public override void AccumulateEnergy(Time now)
    {
        if (now <= Window.LastEnergyUpdateTime)
            return;

        if (Session is not null)
            Window = Window with { DeliveredKWh = Window.DeliveredKWh + Session.GetDeliveredKWh(Window.LastEnergyUpdateTime, now) };

        Window = Window with { LastEnergyUpdateTime = now };
    }

    /// <inheritdoc/>
    public override void UpdateWindowStats()
    {
        var isActive = Queue.Count > 0
            || Session is not null
            || Window.DeliveredKWh > 0;

        if (!isActive)
            return;

        Window = Window with
        {
            HadActivity = true,
            QueueSize = Queue.Count,
        };
    }

    public IReadOnlyList<ConnectedEV> CreateConnectedEVs(Time currentTime)
    {
        if (Session is null)
            return [.. Queue];

        var activeSession = Session;

        var activeEv = activeSession.EV with
        {
            CurrentSoC = activeSession.GetCurrentSoC(currentTime)
        };

        return [activeEv, .. Queue];
    }
}
