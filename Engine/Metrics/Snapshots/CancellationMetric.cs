namespace Engine.Metrics.Snapshots;

/// <summary>
/// Collects snapshots of reservation and cancellation requests made during the simulation.
/// </summary>
public class CancellationMetric
{
    /// <summary>
    /// Gets the list of cancellation request snapshots.
    /// </summary>
    public List<CancellationSnapshot> Cancellations { get; } = [];
}