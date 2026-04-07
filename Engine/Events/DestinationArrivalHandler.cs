namespace Engine.Events;

using Engine.Metrics.Events;
using Engine.Vehicles;
using Engine.Metrics;

/// <summary>
/// Handles the <see cref="ArriveAtDestination"/> event by collecting a <see cref="ArrivalAtDestinationMetric"/> for the EV that arrived at its destination,
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
        ref var ev = ref evStore.Get(e.EVId);

        // var metric = ArrivalAtDestinationMetric.Collect(ref ev, e.Time);
        // metrics.RecordArrival(metric);
        Console.WriteLine($"EV {e.EVId} arrived at destination at time {e.Time}. Final state: {ev}");
        evStore.Free(e.EVId);
    }
}
