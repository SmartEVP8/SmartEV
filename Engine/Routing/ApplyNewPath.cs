namespace Engine.Routing;

using Core.Shared;
using Core.Charging;
using Core.Vehicles;
using Engine.Utils;

/// <summary>
/// Calculates detour deviations by querying OSRM routes.
/// </summary>
public class ApplyNewPath(IDestinationRouter router)
{
    private readonly IDestinationRouter _router = router;

    /// <summary>
    /// Fetches the detour route from the EV's current position through the station to the destination,
    /// decodes the polyline, and splices it into the EV's journey.
    /// </summary>
    /// <param name="ev">The EV to reroute.</param>
    /// <param name="station">The station the EV should reroute through.</param>
    /// <param name="currentTime">Used to determine the EV's current position in the journey.</param>
    /// <param name="durationToStation">The duration to the station, passed from FindCandidate.</param>
    public void ApplyNewPathToEV(ref EV ev, Station station, Time currentTime, Time durationToStation)
    {
        var currentPos = ev.Journey.CurrentPosition(currentTime);
        var destination = ev.Journey.Path.Waypoints.Last();

        var (duration, polyline) = _router.QueryDestination(
        [
            currentPos.Longitude, currentPos.Latitude,
            station.Position.Longitude, station.Position.Latitude,
            destination.Longitude, destination.Latitude,
        ]);

        var detourPath = Polyline6ToPoints.DecodePolyline(polyline);

        var durationStationDestination = duration - durationToStation;
        
        var stationWaypoint = detourPath.Waypoints
            .MinBy(w => Math.Pow(w.Longitude - station.Position.Longitude, 2)
                      + Math.Pow(w.Latitude - station.Position.Latitude,  2));

        var stationIndex = detourPath.Waypoints.IndexOf(stationWaypoint);
        
        var waypointsToStation = new Paths([currentPos, .. detourPath.Waypoints[..stationIndex]]);
        var waypointsToDestination = new Paths([station.Position, .. detourPath.Waypoints[stationIndex..]]);

        var roundedDuration = (uint)Math.Ceiling(duration / 60);
        ev.Journey.UpdateRoute(waypointsToStation, currentTime, (Time)roundedDuration);
        ev.Journey.UpdateStationToDestinationRoute(waypointsToDestination, (Time)(uint)durationStationDestination);
    }
}