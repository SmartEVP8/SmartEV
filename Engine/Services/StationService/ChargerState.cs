namespace Engine.Services;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Shared;
using Engine.Metrics.Snapshots;

/// <summary>
/// Tracks the runtime state of a charger, active sessions, waiting queue, and window metrics.
/// </summary>
public class ChargerState(ChargerBase charger, ushort stationId)
{
    /// <summary>
    /// Gets the charger this state belongs to.
    /// </summary>
    public ChargerBase Charger { get; } = charger;

    /// <summary>
    /// Gets the id of the station this charger belongs to for metrics tagging.
    /// </summary>
    public ushort StationId { get; } = stationId;

    /// <summary>
    /// Gets the queue of EVs waiting to charge at this charger, in order of arrival.
    /// </summary>
    public Queue<(int EVId, ConnectedEV EV)> Queue { get; } = new();

    /// <summary>
    /// Gets or sets the active charging session at side A, or null if free. Always used for single chargers.
    /// </summary>
    public ActiveSession? SessionA { get; set; }

    /// <summary>
    /// Gets or sets the active charging session at side B, or null if free. Always null for single chargers.
    /// </summary>
    public ActiveSession? SessionB { get; set; }

    /// <summary>
    /// Gets or sets the per-window metrics for this charger.
    /// </summary>
    public ChargerWindow Window { get; set; }

    /// <summary>
    /// Gets a value indicating whether the charger has at least one free side.
    /// </summary>
    public bool IsFree => Charger switch
    {
        SingleCharger => SessionA is null,
        DualCharger => SessionA is null || SessionB is null,
        _ => false
    };

    /// <summary>
    /// Accumulates exact energy using the pre-calculated charging curve trajectory.
    /// </summary>
    /// <param name="simNow">The current simulation time to accumulate energy up to.</param>
    public void AccumulateEnergy(Time simNow)
    {
        if (simNow <= Window.LastEnergyUpdateTime) return;

        if (SessionA is not null)
            Window = Window with { DeliveredKWh = Window.DeliveredKWh + SessionA.GetDeliveredKWh(Window.LastEnergyUpdateTime, simNow) };

        if (SessionB is not null)
            Window = Window with { DeliveredKWh = Window.DeliveredKWh + SessionB.GetDeliveredKWh(Window.LastEnergyUpdateTime, simNow) };

        Window = Window with { LastEnergyUpdateTime = simNow };
    }
}