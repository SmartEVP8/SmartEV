namespace Engine.Routing;

using Core.Shared;
using Core.Charging;
using Core.Vehicles;
using Engine.Utils;

/// <summary>
/// Calculates detour deviations by querying OSRM routes.
/// </summary>
public class ApplyNewPath(IDestinationRouter router) : IApplyNewPath
{
    private readonly IDestinationRouter _router = router;

    /// <summary>
    /// Fetches the detour route from the EV's current position through the station to the destination,
    /// decodes the polyline, and splices it into the EV's journey.
    /// </summary>
    /// <param name="ev">The EV to reroute.</param>
    /// <param name="station">The station the EV should reroute through.</param>
    /// <param name="currentTime">Used to determine the EV's current position in the journey.</param>
    public Position ApplyNewPathToEV(ref EV ev, Station station, Time currentTime)
    {
        var originalRouteRightNow = ev.Journey.GetPathFromCurrentPosition(currentTime);
        var currentPos = originalRouteRightNow.Waypoints[0];
        var destination = originalRouteRightNow.Waypoints[^1];

        var res = _router.QueryDestinationWithStop(
            currentPos.Longitude, currentPos.Latitude, station.Position.Longitude, station.Position.Latitude, destination.Longitude, destination.Latitude, station.Id);

        var detourPath = Polyline6ToPoints.DecodePolyline(res.Polyline);

        var decisionPoint = CalculateDecisionPoint(originalRouteRightNow, detourPath);

        // Somehow we got the case where they were equal and we compare dist between points whcih was 0 and gave us division by 0.
        Paths? newWaypoints;
        if (currentPos == detourPath.Waypoints[0])
            newWaypoints = new Paths(detourPath.Waypoints);
        else
            newWaypoints = new Paths([currentPos, .. detourPath.Waypoints]);

        var roundedDuration = (uint)Math.Ceiling(res.Duration);
        ev.Journey.UpdateRoute(newWaypoints, station.Position, currentTime, (Time)roundedDuration, res.Distance / 1000);
        return decisionPoint;
    }

    /// <summary>
    /// Calculates the decision point where the original route and the detour route diverge.
    /// </summary>
    /// <param name="r1">Route 1.</param>
    /// <param name="r2">Route 2.</param>
    /// <returns>The position where <paramref name="r1"/> and <paramref name="r2"/> diverge.</returns>
    /// <exception cref="SkillissueException">If they never diverge.</exception>
    public static Position CalculateDecisionPoint(Paths r1, Paths r2)
    {
        var sharedCount = Math.Min(r1.Waypoints.Count, r2.Waypoints.Count);

        for (var i = 0; i < sharedCount; i++)
        {
            if (r1.Waypoints[i] != r2.Waypoints[i])
            {
                return i == 0 ? r1.Waypoints[0] : r1.Waypoints[i - 1];
            }
        }
        throw new SkillissueException("No decision point found between original route and detour route.");
    }
}
