namespace Engine.Events.Middleware;

using Core.Charging;
using Engine.Grid;
using Engine.Routing;
using Engine.Vehicles;

/// <summary>Service responsible for finding candidate stations for an EV.</summary>
/// <param name="router">Router used to compute paths from the EV's position to candidate stations and the destination.</param>
/// <param name="stations">Stations to choose from.</param>
/// <param name="spatialGrid">Used for pruning of <paramref name="stations"/>.</param>
/// <param name="evStore">Gives access to EV's.</param>
public class FindCandidateStationService(
    IOSRMRouter router,
    Dictionary<ushort, Station> stations,
    SpatialGrid spatialGrid,
    EVStore evStore)
{
    /// <summary>Computes the candidate stations and their travel durations for an EV.</summary>
    /// <param name="e">The <see cref="FindCandidateStations"/> event.</param>
    /// <returns>A dictionary mapping each candidate station to its travel duration.</returns>
    public Dictionary<Station, float> GetCandidateStations(FindCandidateStations e)
    {
        var ev = evStore.Get(e.EVId);
        var stationIds = spatialGrid.GetStationsAlongPolyline(ev.Journey.Path, ev.Preferences.MaxPathDeviation);

        var reachableStationIds = ReachableStations.FindReachableStations(
            ev.Journey.Path, ev, stations, stationIds, ev.Preferences.MaxPathDeviation).ToArray();

        var pos = ev.Journey.CurrentPosition(e.Time);
        var dest = ev.Journey.Path.Waypoints.Last();
        var (durations, _) = router.QueryStationsWithDest(
            pos.Longitude,
            pos.Latitude,
            dest.Longitude,
            dest.Latitude,
            reachableStationIds);
        return reachableStationIds
            .Select(id => stations[id])
            .Zip(durations)
            .ToDictionary(x => x.First, x => x.Second);
    }
}
