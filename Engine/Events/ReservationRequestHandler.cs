namespace Engine.Events;

using Core.Charging;
using Core.Routing;
using Core.Shared;
using Engine.Routing;
using Engine.Metrics.Snapshots;

/// <summary>
/// Handles a reservation request from a connected car to a charging station.
/// </summary>
public class ReservationRequestEventHandler(
    Dictionary<ushort, Station> stations,
    PathDeviator pathDeviator,
    ReservationMetric metrics)
{
    /// <summary>
    /// Handles the reservation request by incrementing the station's expected queue size,
    /// computing the path deviation, and producing a jittered arrival event.
    /// </summary>
    /// <param name="e">The reservation request event.</param>
    /// <param name="journey">The EV's current journey.</param>
    /// <returns>
    /// A scheduled <see cref="ArriveAtStation"/> event, or null if the station is unknown.
    /// </returns>
    public ArriveAtStation? Handle(ReservationRequest e, Journey journey)
    {
        if (!stations.TryGetValue(e.StationId, out var station))
            return null;

        station.ExpectedQueueSize++;

        metrics.RequestTimestamps[e.EVId] = e.Time;
        metrics.TotalRequests++;

        var stationPosition = station.Position;
        var (deviation, _) = pathDeviator.CalculateDetourDeviation(journey, e.Time, stationPosition);
        metrics.PathDeviations[e.EVId] = deviation;

        var addDeviation = 0.8f + ((double)Random.Shared.NextDouble() * 0.4f);
        var arrivalTime = new Time((uint)(e.Time.T * addDeviation));

        return new ArriveAtStation(e.EVId, e.StationId, arrivalTime);
    }
}