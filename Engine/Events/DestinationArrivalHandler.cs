namespace Engine.Events;

using Engine.Metrics.Events;
using Engine.Metrics;
using Core.Vehicles;
using Serilog;

/// <summary>
/// Handles the <see cref="ArriveAtDestination"/> event by collecting a <see cref="ArrivalAtDestinationMetric"/> for the EV that arrived at its destination,
/// recording it via the <see cref="MetricsService"/>.
/// </summary>
/// <param name="metrics">Metrics service.</param>
/// <param name="evs">EV store.</param>
public class DestinationArrivalHandler(
    MetricsService metrics,
    Dictionary<int, EV> evs)
{
    /// <summary>
    /// Handles the <see cref="ArriveAtDestination"/> event.
    /// </summary>
    /// <param name="e">The event.</param>
    public void Handle(ArriveAtDestination e)
    {
        var metric = ArrivalAtDestinationMetric.Collect(e.EV, e.Time);
        metrics.RecordArrival(metric);
        if (!evs.Remove(e.EV.Id))
        {
            var ex = new KeyNotFoundException($"EV with ID {e.EV.Id} not found in EV store when handling arrival at destination.");
            Log.Error(ex, "EV with ID {@EVId} not found in EV store when handling arrival at destination.", e.EV.Id);
            throw ex;
        }
    }
}
