namespace Engine.Routing;

using Core.Shared;
using Core.Vehicles;
using Core.Charging;
using Core.GeoMath;
using Core.Helper;

/// <summary>
/// Provides functionality to find reachable stations for an EV based on its current charge and the distance to nearby stations along a given path.
/// </summary>
public class ReachableStations
{
    /// <summary>
    /// Finds the stations that are reachable by the EV given its current charge.
    /// </summary>
    /// <param name="waypoints">The direct route to the EV's destination.</param>
    /// <param name="ev">The EV looking for a Station to charge at.</param>
    /// <param name="stations">The full Dictionary of stations, that hasnt been altered at all.</param>
    /// <param name="nearbyStations">The list of station ids provided by the spatial grid.</param>
    /// <param name="radius">The same radius used in finding stations in the Spatial Grid.</param>
    /// <returns>Returns a list of ids of stations within reach of the EV.</returns>
    public static List<ushort> FindReachableStations(List<Position> waypoints, EV ev, Dictionary<ushort, Station> stations, List<ushort> nearbyStations, double radius)
    {
        var evBattery = ev.Battery;
        if (evBattery.StateOfCharge <= 0)
            throw Log.Error(0, 0, new InvalidOperationException($"EV {ev} has no charge left, but is trying to find reachable stations. This should not happen."), ("EV", ev));

        var reach = evBattery.StateOfCharge * evBattery.MaxCapacityKWh / ((double)ev.ConsumptionWhPerKm / 1000);
        return [.. nearbyStations.Where(id =>
            {
                var dist = GeoMath.DistancesThroughPath(waypoints, stations[id].Position, radius);
                return dist > -1 && dist <= reach;
            })];
    }

    public static bool ReachesStationWithTooMuchSoC(float distToStation, float distToDestination, ref EV ev, float chargeBufferPercent)
    {
        var socAtStation = (ev.Battery.CurrentChargeKWh - ev.EnergyForDistanceKWh(distToStation)) / ev.Battery.MaxCapacityKWh;
        var expectChargeTarget = ev.CalcPreDesiredComputedSoC(distToDestination) * chargeBufferPercent;
        Console.WriteLine($"EV has SoC {socAtStation:P} at station, expected target SoC is {expectChargeTarget:P} with buffer {chargeBufferPercent:P}. Dist to station: {distToStation} km, dist to dest: {distToDestination} km.");
        return socAtStation > expectChargeTarget;
    }
}
