namespace Engine.Routing;

using Core.Shared;
using Core.Vehicles;
using Core.Charging;
using Core.GeoMath;
using Serilog;

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
        {
            Log.Error("EV {@EV} has no charge left, but is trying to find reachable stations. This should not happen.", ev);
            throw new InvalidOperationException($"EV {ev} has no charge left, but is trying to find reachable stations. This should not happen.");
        }

        var reach = evBattery.StateOfCharge * evBattery.MaxCapacityKWh / ((double)ev.ConsumptionWhPerKm / 1000);
        return [.. nearbyStations.Where(id =>
            {
                var dist = GeoMath.DistancesThroughPath(waypoints, stations[id].Position, radius);
                return dist > -1 && dist <= reach;
            })];
    }

    /// <summary>
    /// Filters the list of reachable stations to those that are actually worth detouring to based on the EV's current charge, the distance to the station, and the distance from the station to the destination.
    /// </summary>
    /// <param name="ev">The EV for which to filter candidates.</param>
    /// <param name="detourLegs">The routing legs for the detour.</param>
    /// <param name="reachableStationIds">The IDs of reachable stations.</param>
    /// <param name="chargeBufferPercent">The percentage of charge to buffer for the detour.</param>
    /// <returns>A dictionary of filtered station IDs and their corresponding durations.</returns>
    public static Dictionary<ushort, (float, float)> FilterCandidates(ref EV ev, RoutingLegsResult detourLegs, ushort[] reachableStationIds, float chargeBufferPercent)
    {
        var result = new Dictionary<ushort, (float, float)>();

        for (var i = 0; i < reachableStationIds.Length; i++)
        {
            var toStationDur = detourLegs.ToStation.Durations[i];
            var toStationDist = detourLegs.ToStation.Distances[i];
            var toDestDur = detourLegs.ToDest.Durations[i];
            var toDestDist = detourLegs.ToDest.Distances[i];

            var energyToStationKWh = ev.EnergyForDistanceKWh(toStationDist / 1000f);
            var chargeAtStationKWh = ev.Battery.CurrentChargeKWh - energyToStationKWh;
            var socAtStation = chargeAtStationKWh / ev.Battery.MaxCapacityKWh;

            var chargingThreshold = ev.CalcPreDesiredComputedSoC(toDestDist) * chargeBufferPercent;

            if (socAtStation >= chargingThreshold)
            {
                Log.Information("Station {StationId} filtered out: SOC at station {SOC:P} is above threshold {Threshold:P}.", reachableStationIds[i], socAtStation, chargingThreshold);
                continue;
            }

            result[reachableStationIds[i]] = (toStationDur, toDestDur);
        }

        return result;
    }
}
