namespace Engine.Events;

using Core.Charging;
using Core.Routing;
using Core.Shared;
using Engine.Routing;
using Engine.Metrics.Snapshots;

/// <summary>
/// Handles a reservation request from an EV to a charging station.
/// </summary>
/// <param name="stations">A dictionary of all known stations, keyed by station ID.</param>
/// <param name="pathDeviator">Calculates the detour deviation added to an EV's journey by routing through a station.</param>
/// <param name="metrics">Records reservation request counts, timestamps, and path deviations.</param>
/// <param name="eventScheduler">Schedules the produced arrival event into the simulation queue.</param>
public class ReservationRequestEventHandler(
    Dictionary<ushort, Station> stations,
    PathDeviator pathDeviator,
    ReservationMetric metrics,
    EventScheduler eventScheduler)
{
    /// <summary>
    /// Handles the reservation request by incrementing the station's expected queue size,
    /// recording metrics, computing the path deviation, and scheduling a jittered arrival event.
    /// </summary>
    /// <param name="e">The reservation request event containing the EV ID, station ID, and request time.</param>
    /// <param name="journey">The EV's current journey, used to calculate the detour deviation.</param>
    /// <remarks>
    /// The produced <see cref="ArriveAtStation"/> event is scheduled with a ±20% deviation
    /// applied to the reservation time to simulate variance in real-world arrival behaviour.
    /// </remarks>
    public void Handle(ReservationRequest e, Journey journey)
    {
        if (!stations.TryGetValue(e.StationId, out var station))
            return;

        station.ExpectedQueueSize++;

        metrics.RequestTimestamps[e.EVId] = e.Time;
        metrics.TotalRequests++;

        var (deviation, _) = pathDeviator.CalculateDetourDeviation(journey, e.Time, station.Position);
        metrics.PathDeviations[e.EVId] = deviation;

        var addDeviation = 0.8f + (float)(Random.Shared.NextDouble() * 0.4);
        var arrivalTime = new Time((uint)(e.Time.T * addDeviation));

        eventScheduler.ScheduleEvent(new ArriveAtStation(e.EVId, e.StationId, arrivalTime));
    }
}