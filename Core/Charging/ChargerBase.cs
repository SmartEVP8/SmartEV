namespace Core.Charging;

using Core.Charging.ChargingModel;
using Core.Shared;

/// <summary>
/// Base class for chargers. Holds identity, power cap, and the queue of waiting EVs.
/// </summary>
/// <param name="id">The id of the charger.</param>
/// <param name="maxPowerKW">The maximum power output in kilowatts.</param>
public abstract class ChargerBase(int id, int maxPowerKW)
{
    /// <summary>Gets the charger identifier.</summary>
    public int Id { get; } = id;

    /// <summary>Gets the maximum power output in kilowatts.</summary>
    public int MaxPowerKW { get; } = maxPowerKW;

    /// <summary>Gets the queue of EV ids waiting to charge at this charger.</summary>
    /// <summary>
    /// Gets the queue of EVs waiting to charge at this charger, in order of arrival.
    /// </summary>
    public Queue<ConnectedEV> Queue { get; } = new();

    /// <summary>
    /// Gets or sets the per-window metrics for this charger.
    /// </summary>
    public ChargerWindow Window { get; set; }

    /// <summary>
    /// Gets a value indicating whether the charger has a free spot.
    /// </summary>
    public abstract bool IsFree { get; }

    /// <summary>
    /// Accumulates exact energy using the pre-calculated charging curve trajectory.
    /// </summary>
    /// <param name="now">The current time to accumulate energy up to.</param>
    public abstract void AccumulateEnergy(Time now);

    /// <summary>
    /// Updates the metrics in the window.
    /// </summary>
    public abstract void UpdateWindowStats();
}
