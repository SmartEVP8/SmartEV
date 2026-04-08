namespace Engine.Routing;

using Core.Shared;
using Core.Charging;
using Core.Vehicles;
using Engine.Utils;

/// <summary>
/// Calculates detour deviations by querying OSRM routes.
/// </summary>
public class EVDetourPlanner(IDestinationRouter router)
{
    private readonly IDestinationRouter _router = router;

    /// <summary>
    /// Fetches the detour route from the EV's current position through the station to the destination,
    /// decodes the polyline, and splices it into the EV's journey.
    /// </summary>
    /// <param name="ev">The EV to reroute.</param>
    /// <param name="station">The station the EV should reroute through.</param>
    /// <param name="currentTime">Used to determine the EV's current position in the journey.</param>
    public void Update(ref EV ev, Station station, Time currentTime)
    {
        var currentPos = ev.Advance(currentTime);
        var destination = ev.Journey.Current.Waypoints.Last();

        var res = _router.QueryDestinationWithStop(
            currentPos.Longitude, currentPos.Latitude, station.Position.Longitude, station.Position.Latitude, destination.Longitude, destination.Latitude, station.Id);

        var detourPath = Polyline6ToPoints.DecodePolyline(res.Polyline);
        var newWaypoints = new List<Position>([currentPos, .. detourPath]);
        var roundedDuration = (uint)Math.Ceiling(res.Duration * Time.MillisecondsPerSecond);
        ev.Journey.UpdateRoute(newWaypoints, station.Position, currentTime, (Time)roundedDuration, res.Distance / 1000);
    }
}
