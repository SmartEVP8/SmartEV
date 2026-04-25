namespace Engine.Events.Middleware;

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
public struct DurToStationAndDest(float DurToStation, float DurToDest, float DistStationToDestMeters = 0f)
{
    /// <summary>Duration from the EV position to the candidate station.</summary>
    public float DurToStation = DurToStation;

    /// <summary>Duration from the candidate station to the destination.</summary>
    public float DurToDest = DurToDest;

    /// <summary>Distance from the candidate station to the destination in meters.</summary>
    public float DistStationToDestMeters = DistStationToDestMeters;
}

/// <summary>Service responsible for pre-computing the candidate stations for an EV and caching the results for later retrieval.</summary>
public class FindCandidateStationService : IFindCandidateStationService
{
    private record StationQuery(Task<Dictionary<ushort, DurToStationAndDest>> Task);

    private readonly IOSRMRouter _router;
    private readonly Dictionary<ushort, Station> _stations;
    private readonly ISpatialGrid _spatialGrid;
    private readonly EVStore _evStore;
    private readonly StationService _stationService;
    private readonly float _chargerBufferPercent;

    private readonly ConcurrentDictionary<int, StationQuery> _evStationPaths = new();
    private readonly PriorityQueue<(FindCandidateStations Event, TaskCompletionSource<Dictionary<ushort, DurToStationAndDest>> Tcs), double> _taskQueue = new();
    private readonly SemaphoreSlim _queueSignal = new(0);
    private readonly Lock _queueLock = new();
    private readonly SemaphoreSlim _osrmConcurrencyLimit;

    /// <summary>Initializes a new instance of the <see cref="FindCandidateStationService"/> class.</summary>
    /// <param name="router">Router used to compute paths from the EV's position to candidate stations and the destination.</param>
    /// <param name="stations">Stations to choose from.</param>
    /// <param name="spatialGrid">Used for pruning of <paramref name="stations"/>.</param>
    /// <param name="evStore">Gives access to EV's.</param>
    /// <param name="stationService">Service for accessing station reservations.</param>
    /// <param name="chargerBufferPercent">Scalar for SoC difference.</param>
    /// <param name="maxConcurrency">The max amount of cores to use.</param>
    public FindCandidateStationService(
        IOSRMRouter router,
        Dictionary<ushort, Station> stations,
        ISpatialGrid spatialGrid,
        EVStore evStore,
        StationService stationService,
        float chargerBufferPercent,
        int maxConcurrency = 4)
    {
        _router = router;
        _stations = stations;
        _spatialGrid = spatialGrid;
        _evStore = evStore;
        _stationService = stationService;
        _chargerBufferPercent = chargerBufferPercent;
        _osrmConcurrencyLimit = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        _ = DispatchLoopAsync();
    }

    private async Task DispatchLoopAsync()
    {
        while (true)
        {
            await _queueSignal.WaitAsync();
            await _osrmConcurrencyLimit.WaitAsync();

            (FindCandidateStations Event, TaskCompletionSource<Dictionary<ushort, DurToStationAndDest>> Tcs) item;
            lock (_queueLock)
            {
                item = _taskQueue.Dequeue();
            }

            _ = Task.Run(() =>
            {
                try
                {
                    var result = ComputeCandidates(item.Event);
                    item.Tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    item.Tcs.SetException(ex);
                }
                finally
                {
                    _osrmConcurrencyLimit.Release();
                }
            });
        }
    }

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

            var tcs = new TaskCompletionSource<Dictionary<ushort, DurToStationAndDest>>(TaskCreationOptions.RunContinuationsAsynchronously);

            _evStationPaths[e.EVId] = new StationQuery(tcs.Task);

            lock (_queueLock)
            {
                _taskQueue.Enqueue((e, tcs), e.Time);
            }

            _queueSignal.Release();
        };
    }

    private Dictionary<ushort, DurToStationAndDest> ComputeCandidates(FindCandidateStations e, double PathdeviationMultiplier = 1.0)
    {
        ref var ev = ref _evStore.Get(e.EVId);
        var pos = ev.Advance(e.Time) ?? throw Log.Error(e.EVId, e.Time, new SkillissueException($"EV {e.EVId} has no position at time {e.Time} after advancing. This should not happen."));

        var pathDeviationMultiplied = ev.Preferences.MaxPathDeviation * PathdeviationMultiplier;

        var filteredStationIds = _spatialGrid.GetStationsAlongPolyline(
            ev.Journey.Current.Waypoints,
            pathDeviationMultiplied);
        if (filteredStationIds.Count > 0)
        {
            var reachableStationIds = ReachableStations.FindReachableStations(
                ev.Journey.Current.Waypoints,
                ev,
                _stations,
                filteredStationIds,
                pathDeviationMultiplied).ToArray();

            var destination = ev.Journey.Current.Waypoints.Last();
            var detourResult = _router.QueryStationsWithDest(
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

                if (ev.CheckIfTargetSoCIsLowerThanCurrentSoC(toStation.Item2, toDestination.Item2, _chargerBufferPercent))
                    continue;

                refinedCandidateDurations[stationId] = new DurToStationAndDest(toStation.Item1, toDestination.Item1, toDestination.Item2);
            }

            if (refinedCandidateDurations.Count == 0 && _stationService.GetReservationStationId(e.EVId) is ushort && ev.DistanceOnCurrentChargeKm() > pathDeviationMultiplied)
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

        if (_stationService.GetReservationStationId(e.EVId) is ushort && ev.DistanceOnCurrentChargeKm() > pathDeviationMultiplied)
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
        if (!_evStationPaths.TryRemove(evId, out var query))
            throw Log.Error(evId, 0, new SkillissueException($"No pre-computed station query found for EV {evId}. Ensure PreComputeCandidateStation is called first."));
        return await query.Task;
    }
}
