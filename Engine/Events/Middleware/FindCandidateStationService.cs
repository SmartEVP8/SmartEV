namespace Engine.Events.Middleware;

using Core.Charging;
using Engine.Grid;
using Engine.Routing;
using Engine.Services;
using Engine.Utils;
using Engine.Vehicles;
using Core.Helper;

/// <summary>
/// Represents travel durations from an EV position to a candidate station and then to the destination.
/// </summary>
/// <param name="DurToStation">Duration from the EV position to the candidate station.</param>
/// <param name="DurToDest">Duration from the candidate station to the destination.</param>
/// <param name="DistStationToDestMeters">Distance from the candidate station to the destination in meters.</param>
/// <param name="DistToStationMeters">Distance from the EV position to the candidate station in meters.</param>
public struct DurToStationAndDest(float DurToStation, float DurToDest, float DistStationToDestMeters, float DistToStationMeters)
{
    /// <summary>Duration from the EV position to the candidate station.</summary>
    public float DurToStation = DurToStation;

    /// <summary>Duration from the candidate station to the destination.</summary>
    public float DurToDest = DurToDest;

    /// <summary>Distance from the candidate station to the destination in meters.</summary>
    public float DistStationToDestMeters = DistStationToDestMeters;

    /// <summary>Distance from the EV position to the candidate station in meters.</summary>
    public float DistToStationMeters = DistToStationMeters;
}

/// <summary>Service responsible for pre-computing the candidate stations for an EV and caching the results for later retrieval.</summary>
/// <param name="router">Router used to compute paths from the EV's position to candidate stations and the destination.</param>
/// <param name="stations">Stations to choose from.</param>
/// <param name="spatialGrid">Used for pruning of <paramref name="stations"/>.</param>
/// <param name="evStore">Gives access to EV's.</param>
/// <param name="stationService">Used to check for existing reservations at stations.</param>
/// <param name="settings">Used to get the charge buffer percent for candidate filtering.</param>
public class FindCandidateStationService(
    IOSRMRouter router,
    Dictionary<ushort, Station> stations,
    ISpatialGrid spatialGrid,
    EVStore evStore,
    StationService stationService,
    Init.EngineSettings settings) : IFindCandidateStationService
{
    private record StationQuery(Task<Dictionary<ushort, DurToStationAndDest>> Task);

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
                throw Log.Error(0, 0, new SkillissueException("Not the correct event type"), ("Event", fcse));

            _evStationPaths[e.EVId] = new StationQuery(Task.Run(() => ComputeCandidates(e)));
        };
    }

    private Dictionary<ushort, DurToStationAndDest> ComputeCandidates(FindCandidateStations e, double PathdeviationMultiplier = 1.0)
    {
        ref var ev = ref evStore.Get(e.EVId);
        var pos = ev.Advance(e.Time) ?? throw Log.Error(e.EVId, e.Time, new SkillissueException($"EV {e.EVId} has no position at time {e.Time} after advancing. This should not happen."));

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

            var refinedCandidateDurations = new Dictionary<ushort, DurToStationAndDest>(reachableStationIds.Length);
            for (var i = 0; i < reachableStationIds.Length; i++)
            {
                var stationId = reachableStationIds[i];
                var detourDistanceMeters = detourResult.TotalDistance(i);
                if (detourDistanceMeters < 0 || float.IsNaN(detourDistanceMeters))
                    continue;

                var toStation = (detourResult.ToStation.Durations[i], detourResult.ToStation.Distances[i]);
                var toDestination = (detourResult.ToDest.Durations[i], detourResult.ToDest.Distances[i]);
                if (!ev.CanReachToStation(toStation.Item2 / 1000f, ev.Preferences.MinAcceptableCharge))
                    continue;

                if (ev.SoCUsedAfterADistance(toStation.Item2 / 1000) <= 0)
                    continue;

                if (ev.CheckIfTargetSoCIsLowerThanCurrentSoC(toStation.Item2, toDestination.Item2, settings.ChargeBufferPercent))
                    continue;

                refinedCandidateDurations[stationId] = new DurToStationAndDest(toStation.Item1, toDestination.Item1, toDestination.Item2, toStation.Item2);
            }

            if (refinedCandidateDurations.Count == 0 && stationService.GetReservationStationId(e.EVId) is ushort && ev.DistanceOnCurrentChargeKm() > pathDeviationMultiplied)
            {
                refinedCandidateDurations = ComputeCandidates(e, PathdeviationMultiplier * 1.25);
            }
            else if (refinedCandidateDurations.Count == 0)
            {
                Log.Warn(e.EVId, e.Time, $"No candidate stations found for EV {e.EVId} at time {e.Time}.");
            }
            else
            {
                Log.Verbose(e.EVId, e.Time, $"Computed candidate stations for EV {e.EVId}: amount of stations: {refinedCandidateDurations.Count} at time {e.Time}.");
            }

            return refinedCandidateDurations;
        }

        if (stationService.GetReservationStationId(e.EVId) is ushort && ev.DistanceOnCurrentChargeKm() > pathDeviationMultiplied)
        {
            return ComputeCandidates(e, PathdeviationMultiplier * 1.25);
        }

        Log.Warn(e.EVId, e.Time, $"No candidate stations found for EV {e.EVId}. Could not find any stations along polyline, even after expanding search radius. Returning empty candidate set.  at time {e.Time}");
        return [];
    }

    /// <summary>Gets the pre-computed candidate stations. Awaits result if it's not yet ready.</summary>
    /// <param name="evId">The EV's id.</param>
    /// <returns>The pre-computed candidate stations.</returns>
    /// <exception cref="SkillissueException">If you try and get a cached candidate which was never precomputed.</exception>
    public async Task<Dictionary<ushort, DurToStationAndDest>> GetCandidateStationFromCache(int evId)
    {
        if (!_evStationPaths.TryGetValue(evId, out var query))
            throw Log.Error(evId, 0, new SkillissueException($"No pre-computed station query found for EV {evId}. Ensure PreComputeCandidateStation is called first."));

        _evStationPaths.Remove(evId);
        return await query.Task;
    }
}
