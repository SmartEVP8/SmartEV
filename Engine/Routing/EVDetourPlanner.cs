namespace Engine.Routing;

using Core.Shared;
using Core.Charging;
using Core.Vehicles;
using Engine.Utils;

/// <summary>
/// Calculates detour deviations by querying OSRM routes.
/// </summary>
public class EVDetourPlanner(IDestinationRouter router) : IEVDetourPlanner
{
    private readonly IDestinationRouter _router = router;

    /// <inheritdoc/>
    public void Update(ref EV ev, Station station, Time currentTime)
    {
        var currentPos = ev.Advance(currentTime);
        var destination = ev.Journey.Current.Waypoints.Last();

        var res = _router.QueryDestinationWithStop(
            currentPos.Longitude,
            currentPos.Latitude,
            station.Position.Longitude,
            station.Position.Latitude,
            destination.Longitude,
            destination.Latitude,
            station.Id);

        var detourPath = Polyline6ToPoints.DecodePolyline(res.Polyline);
        var newWaypoints = new List<Position>([currentPos, .. detourPath]);
        var roundedDuration = (uint)Math.Ceiling(res.Duration);
        ev.Journey.UpdateRoute(newWaypoints, station.Position, currentTime, (Time)roundedDuration, res.Distance / 1000, station.Id);
    }
}
