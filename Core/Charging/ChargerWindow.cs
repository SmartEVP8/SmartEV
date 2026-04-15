namespace Core.Charging;

using Core.Shared;

/// <summary>
/// Tracks per-window metrics for a charger, reset at each snapshot interval.
/// </summary>
public struct ChargerWindow
{
    /// <summary>
    /// Gets or sets the energy delivered in the current snapshot window in kWh.
    /// </summary>
    public double DeliveredKWh { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this charger had any activity during the current snapshot window.
    /// </summary>
    public bool HadActivity { get; set; }

    /// <summary>
    /// Gets or sets the queue size seen during the current snapshot window.
    /// </summary>
    public int QueueSize { get; set; }

    /// <summary>
    /// Gets or sets the last simulation time the energy accumulator was updated.
    /// </summary>
    public Time LastEnergyUpdateTime { get; set; }

    /// <summary>
    /// Resets the window metrics for the next snapshot interval.
    /// </summary>
    /// <param name="currentQueueSize">The current queue size at reset time.</param>
    public void Reset(int currentQueueSize)
    {
        DeliveredKWh = default;
        HadActivity = default;
        QueueSize = currentQueueSize;
    }
}
