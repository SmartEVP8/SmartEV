namespace Engine.Events;

using Engine.Metrics.Events;
using Engine.Vehicles;
using Engine.Metrics;

/// <summary>
/// Handles the <see cref="ArriveAtDestination"/> event by collecting a <see cref="DeadlineMetric"/> for the EV that arrived at its destination, 
/// recording it via the <see cref="MetricsService"/>, and freeing the EV from the <see cref="EVStore"/>.
/// </summary>
/// <param name="metrics">Metrics service.</param>
/// <param name="evStore">EV store.</param>
public class DestinationArrivalHandler(
    MetricsService metrics,
    EVStore evStore)
{
    /// <summary>
    /// Handles the <see cref="ArriveAtDestination"/> event.
    /// </summary>
    /// <param name="e">The event.</param>
    public void Handle(ArriveAtDestination e)
    {
        var ev = evStore.Get(e.EVId);

        var metric = DeadlineMetric.Collect(ref ev, e.Time);
        metrics.RecordDeadline(metric);

        evStore.Free(e.EVId);
    }
}