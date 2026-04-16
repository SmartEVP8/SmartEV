namespace Engine.Events.Middleware;

using Core.Charging;
using Engine.Grid;
using Engine.Routing;
using Engine.Services;
using Engine.Utils;
using Engine.Vehicles;
using Serilog;

/// <summary>Service responsible for pre-computing the candidate stations for an EV and caching the results for later retrieval.</summary>
/// <param name="router">Router used to compute paths from the EV's position to candidate stations and the destination.</param>
/// <param name="stations">Stations to choose from.</param>
/// <param name="spatialGrid">Used for pruning of <paramref name="stations"/>.</param>
/// <param name="evStore">Gives access to EV's.</param>
public class FindCandidateStationService(
    IOSRMRouter router,
    Dictionary<ushort, Station> stations,
    ISpatialGrid spatialGrid,
    EVStore evStore,
    StationService stationService) : IFindCandidateStationService
{
    private record StationQuery(Task<Dictionary<ushort, float>> Task);

    private readonly Dictionary<int, StationQuery> _evStationPaths = [];

    /// <summary>
    /// Computes the calculation of the path calculations from an EV's position to its relevant stations.
    /// </summary>
    /// <exception cref="SkillissueException">If the passed MiddlewareEvent is not a FindCandidateStations.</exception>
    /// <returns>An action that computes the candidate stations for an EV and caches the results for later retrieval.</returns>
    public Action<IMiddlewareEvent> PreComputeCandidateStation()
    {
        return fcse =>
        {
            if (fcse is not FindCandidateStations e)
                throw new SkillissueException("Not the correct event type");

            _evStationPaths[e.EVId] = new StationQuery(Task.Run(() => ComputeCandidates(e)));
        };
    }

    private Dictionary<ushort, float> ComputeCandidates(FindCandidateStations e, double PathdeviationMultiplier = 1.0)
    {
        ref var ev = ref evStore.Get(e.EVId);
        var pos = ev.Advance(e.Time) ?? throw new SkillissueException($"EV {e.EVId} has no position at time {e.Time} after advancing. This should not happen.");

        var pathDeviationMultiplied = ev.Preferences.MaxPathDeviation * PathdeviationMultiplier;

        var filteredStationIds = spatialGrid.GetStationsAlongPolyline(
            ev.Journey.Current.Waypoints,
            pathDeviationMultiplied);
        if (filteredStationIds.Count > 0)
        {
            var reachableStationIds = ReachableStations.FindReachableStations(
                ev.Journey.Current.Waypoints,
                ev,
                stations,
                filteredStationIds,
                pathDeviationMultiplied).ToArray();

            var destination = ev.Journey.Current.Waypoints.Last();
            var detourResult = router.QueryStationsWithDest(
                pos.Longitude,
                pos.Latitude,
                destination.Longitude,
                destination.Latitude,
                reachableStationIds);

            var refinedCandidateDurations = new Dictionary<ushort, float>(reachableStationIds.Length);
            var baselineDirectDistanceKm = ev.Journey.Current.DistanceKm;

            for (var i = 0; i < reachableStationIds.Length; i++)
            {
                var stationId = reachableStationIds[i];
                var detourDistanceMeters = detourResult.Distances[i];
                if (detourDistanceMeters < 0 || float.IsNaN(detourDistanceMeters))
                    continue;

                if (!ev.CanReachViaDetour(detourDistanceMeters / 1000f, baselineDirectDistanceKm, ev.Preferences.MinAcceptableCharge))
                    continue;

                refinedCandidateDurations[stationId] = detourResult.Durations[i];
            }

            if (refinedCandidateDurations.Count == 0 && stationService.GetReservationStationId(e.EVId) is ushort && ev.DistanceOnCurrentChargeKm() > pathDeviationMultiplied)
            {
                refinedCandidateDurations = ComputeCandidates(e, PathdeviationMultiplier * 1.25);
            }
            else if (refinedCandidateDurations.Count == 0)
            {
                LogHelper.Warn(e.EVId, e.Time, $"No candidate stations found for EV {e.EVId} at time {e.Time}.");
            }
            else
            {
                LogHelper.Verbose(e.EVId, e.Time, $"Computed candidate stations for EV {e.EVId}: amount of stations: {refinedCandidateDurations.Count} at time {e.Time}.");
            }

            return refinedCandidateDurations;
        }

        if (stationService.GetReservationStationId(e.EVId) is ushort && ev.DistanceOnCurrentChargeKm() > pathDeviationMultiplied)
        {
            return ComputeCandidates(e, PathdeviationMultiplier * 1.25);
        }

        LogHelper.Warn(e.EVId, e.Time, $"No candidate stations found for EV {e.EVId}. Could not find any stations along polyline, even after expanding search radius. Returning empty candidate set.  at time {e.Time}");
        return [];
    }

    /// <summary>Gets the pre-computed candidate stations. Awaits result if it's not yet ready.</summary>
    /// <param name="evId">The EV's id.</param>
    /// <returns>The pre-computed candidate stations.</returns>
    /// <exception cref="SkillissueException">If you try and get a cached candidate which was never precomputed.</exception>
    public async Task<Dictionary<ushort, float>> GetCandidateStationFromCache(int evId)
    {
        if (!_evStationPaths.TryGetValue(evId, out var query))
            throw new SkillissueException($"No pre-computed station query found for EV {evId}. Ensure PreComputeCandidateStation is called first.");

        _evStationPaths.Remove(evId);
        return await query.Task;
    }
}
