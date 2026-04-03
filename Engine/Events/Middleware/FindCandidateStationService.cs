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
    private record StationQuery(Task<(ushort[] Stations, float[] Durations)> Task);

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

            _evStationPaths[fcse.EVId] = new StationQuery(Task.Run(async () =>
            {
                var ev = evStore.Get(fcse.EVId);
                var stationIds = spatialGrid.GetStationsAlongPolyline(
                    ev.Journey.Current.Waypoints, ev.Preferences.MaxPathDeviation);
                var reachableStationIds = ReachableStations.FindReachableStations(
                    ev.Journey.Current.Waypoints,
                    ev,
                    stations,
                    stationIds,
                    ev.Preferences.MaxPathDeviation).ToArray();

                var pos = ev.Journey.GetCurrentPosition(fcse.Time);
                var dest = ev.Journey.Current.Waypoints.Last();

                var res = router.QueryStationsWithDest(
                    pos.Longitude,
                    pos.Latitude,
                    dest.Longitude,
                    dest.Latitude,
                    reachableStationIds);

                return (reachableStationIds, res.Durations);
            }));
        };
    }

    /// <summary>Gets the pre-computed candidate stations. Awaits result if it's not yet ready.</summary>
    /// <param name="evId">The EV's id.</param>
    /// <returns>The pre-computed candidate stations.</returns>
    /// <exception cref="SkillissueException">If you try and get a cached candidate which was never precomputed.</exception>
    public async Task<Dictionary<ushort, float>> ComputeCandidateStationFromCache(int evId)
    {
        if (!_evStationPaths.TryGetValue(evId, out var query))
            throw new SkillissueException($"No pre-computed station query found for EV {evId}. Ensure PreComputeCandidateStation is called first.");

        _evStationPaths.Remove(evId);

        var (stationArray, durations) = await query.Task;
        return stationArray.Zip(durations).ToDictionary(x => x.First, x => x.Second);
    }
}
