namespace Engine.Events.Middleware;

using Core.Charging;
using Engine.Grid;
using Engine.Routing;
using Engine.Utils;
using Engine.Vehicles;

/// <summary>Service responsible for pre-computing the candidate stations for an EV and caching the results for later retrieval.</summary>
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
    private record StationQuery(Station[] Stations, (float[] Durations, float[] Distances) Result);
    private readonly Dictionary<int, StationQuery> _evStationPaths = [];

    /// <summary>
    /// Computes the calculation of the path calculations from an EV's position to its relevant stations.
    /// </summary>
    /// <exception cref="SkillissueException">If the passed MiddlewareEvent is not a FindCandidateStations.</exception>
    /// <returns>An action that computes the candidate stations for an EV and caches the results for later retrieval.</returns>
    public Action<IMiddlewareEvent> PreComputeCandidateStation()
    {
        return e =>
        {
            if (e is not FindCandidateStations fcse)
                throw new SkillissueException("Not the correct event type");

            var ev = evStore.Get(fcse.EVId);
            var stationIds = spatialGrid.GetStationsAlongPolyline(ev.Journey.Path, ev.Preferences.MaxPathDeviation);
            var reachableStationsIds = ReachableStations.FindReachableStations(
                ev.Journey.Path,
                ev,
                stations,
                stationIds,
                ev.Preferences.MaxPathDeviation).ToArray();

            var pos = ev.Journey.CurrentPosition(fcse.Time);
            var dest = ev.Journey.Path.Waypoints.Last();

            var result = router.QueryStationsWithDest(
                pos.Longitude,
                pos.Latitude,
                dest.Longitude,
                dest.Latitude,
                reachableStationsIds);

            _evStationPaths[fcse.EVId] = new StationQuery(
                [.. reachableStationsIds.Select(id => stations[id])],
                result);
        };
    }

    /// <summary>Gets the pre-computed candidate stations.</summary>
    /// <param name="evId">The EV's id.</param>
    /// <returns>The pre-computed candidate stations.</returns>
    /// <exception cref="SkillissueException">If you try and get a cached candidate which was never precomputed.</exception>
    public Dictionary<Station, float> ComputeCandidateStationFromCache(int evId)
    {
        if (!_evStationPaths.TryGetValue(evId, out var query))
            throw new SkillissueException($"No pre-computed station query found for EV {evId}. Ensure PreComputeCandidateStation is called first.");

        var (stationIds, result) = query;
        var (durations, _) = result;
        return stationIds.Zip(durations).ToDictionary(x => x.First, x => x.Second);
    }
}
