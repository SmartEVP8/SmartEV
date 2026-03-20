namespace Engine.Routing;

using Core.Shared;
using Core.Vehicles;
using Core.Charging;
using Engine.GeoMath;
public class ReachableStations
{
    /// <summary>
    /// Finds the stations that are reachable by the EV given its current charge.
    /// </summary>
    /// <param name="path">The direct route to the EV's destination.</param>
    /// <param name="ev">The EV looking for a Station to charge at.</param>
    /// <param name="stations">The full Dictionary of stations, that hasnt been altered at all.</param>
    /// <param name="nearbyStations">The list of station ids provided by the spatial grid.</param>
    /// <param name="radius">The same radius used in finding stations in the Spatial Grid.</param>
    /// <returns>Returns a list of ids of stations within reach of the EV.</returns>
    public static List<ushort> FindReachableStations(Paths path, EV ev, Dictionary<ushort, Station> stations, List<ushort> nearbyStations, double radius)
    {
        var evBattery = ev.Battery;
        var reach = (double)evBattery.CurrentCharge / ((double)ev.Efficiency / 1000);
        return nearbyStations.Where(id =>
            {
                var dist = GeoMath.DistancesThroughPath(path, stations[id].Position, radius);
                return dist > -1 && dist <= reach;
            }).ToList();
    }
}
