namespace Engine.Routing;

using Core.Charging;
using Core.Shared;
using Core.Vehicles;

/// <summary>
/// Calculates detour deviations by querying OSRM routes.
/// </summary>
public interface IEVDetourPlanner
{
    /// <summary>
    /// Fetches the detour route from the EV's current position through the station to the destination,
    /// decodes the polyline, and splices it into the EV's journey.
    /// </summary>
    /// <param name="ev">The EV to reroute.</param>
    /// <param name="station">The station the EV should reroute through.</param>
    /// <param name="currentTime">Used to determine the EV's current position in the journey.</param>
    void Update(ref EV ev, Station station, Time currentTime);
}
