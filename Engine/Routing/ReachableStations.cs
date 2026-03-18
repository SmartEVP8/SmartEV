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
    /// <param name="stations">The full list of stations.</param>
    /// <param name="nearbyStations">The list of station ids provided by the spatial grid.</param>
    /// <returns>Returns a list of ids of stations within reach of the EV.</returns>
    public static List<ushort> FindReachableStations(Paths path, EV ev, List<Station> stations, List<ushort> nearbyStations)
    {
        var evConfig = ev.GetConfig();
        var evBattery = ev.GetBattery();
        var reach = (double)evBattery.CurrentCharge / (double)(evConfig.Efficiency / 1000);
        return [.. nearbyStations.Where(id =>
        {
            var station = stations.First(s => s.GetId() == id);
            var distanceToStation = GeoMath.HaversineDistance(path.Waypoints[0], station.Position);
            return distanceToStation <= reach;
        })];
    }
}
