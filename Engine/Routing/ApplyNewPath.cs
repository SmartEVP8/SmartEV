namespace Engine.Routing;

using Core.Shared;
using Core.Charging;
using Core.Vehicles;
using Engine.Utils;

/// <summary>
/// Calculates detour deviations by querying OSRM routes.
/// </summary>
public class ApplyNewPath(IOSRMRouter router)
{
    private readonly IOSRMRouter _router = router;
    
    /// <summary>
    /// Fetches the detour route from the EV's current position through the station to the destination,
    /// decodes the polyline, and splices it into the EV's journey.
    /// </summary>
    /// <param name="ev">The EV to reroute.</param>
    /// <param name="station">The station the EV should reroute through.</param>
    /// <param name="currentTime">Used to determine the EV's current position in the journey.</param>
    public void ApplyNewPathToEV(ref EV ev, Station station, Time currentTime)
    {
        var currentPos = ev.Journey.CurrentPosition(currentTime);
        var destination = ev.Journey.Path.Waypoints.Last();
        var timeTravelled = ev.Journey.TimeElapsed(currentTime);

        var (duration, polyline) = _router.QueryDestination(
        [
            currentPos.Longitude, currentPos.Latitude,
            station.Position.Longitude, station.Position.Latitude,
            destination.Longitude, destination.Latitude,
        ]);

        var detourPath = Polyline6ToPoints.DecodePolyline(polyline);

        var percentageCompleted = (double)timeTravelled / (double)ev.Journey.JourneyDuration;

        var segments = ev.Journey.Path.Waypoints
            .Zip(ev.Journey.Path.Waypoints.Skip(1))
            .Select((p, i) => (
                Index: i,
                Length: Math.Sqrt(
                    Math.Pow(p.Second.Latitude - p.First.Latitude, 2) +
                    Math.Pow(p.Second.Longitude - p.First.Longitude, 2))
            )).ToList();

        var totalLength = segments.Sum(s => s.Length);
        var distanceTraveled = percentageCompleted * totalLength;

        var distanceCovered = 0.0;
        var waypointIndex = 0;
        foreach (var (index, length) in segments)
        {
            if (distanceCovered + length >= distanceTraveled)
            {
                waypointIndex = index + 1;
                break;
            }

            distanceCovered += length;
        }

        var pastJourney = ev.Journey.Path.Waypoints.Take(waypointIndex).ToList();

        List<Position> newWaypoints = [..pastJourney, currentPos, ..detourPath.Waypoints];

        var newJourneyDuration = timeTravelled + duration;

        ev.Journey.UpdatePath(new Paths(newWaypoints));
        ev.Journey.UpdateJourneyDuration((Time)(uint)newJourneyDuration);
    }
}
