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
    public void ApplyNewPathToEV(ref EV ev, Station station, Time currentTime)
    {
        var originalRouteRightNow = ev.Journey.GetPathFromCurrentPosition(currentTime);
        var currentPos = originalRouteRightNow.Waypoints[0];
        var destination = originalRouteRightNow.Waypoints[^1];

        var res = _router.QueryDestination(
        [
            currentPos.Longitude, currentPos.Latitude,
            station.Position.Longitude, station.Position.Latitude,
            destination.Longitude, destination.Latitude,
        ]);

        var detourPath = Polyline6ToPoints.DecodePolyline(res.Polyline);

        var decisionPoint = CalculateDecisionPoint(originalRouteRightNow, detourPath);
        var newWaypoints = new Paths([currentPos, .. detourPath.Waypoints]);
        var roundedDuration = (uint)Math.Ceiling(res.Duration);
        ev.Journey.UpdateRoute(newWaypoints, station.Position, currentTime, (Time)roundedDuration, res.Distance / 1000);
    }

    public Position CalculateDecisionPoint(Paths originalRoute, Paths detourRoute)
    {
        for (int i = 0; i < originalRoute.Waypoints.Count; i++)
        {
            if (originalRoute.Waypoints[i] == detourRoute.Waypoints[i])
            {
                return originalRoute.Waypoints[i];
            }
        }

        throw new SkillissueException("No decision point found between original route and detour route.");
    }
}
