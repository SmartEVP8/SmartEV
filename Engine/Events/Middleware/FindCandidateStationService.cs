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
using Serilog;

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
public class FindCandidateStationService : IFindCandidateStationService
{
    private record StationQuery(Task<Dictionary<ushort, DurToStationAndDest>> Task);

    private readonly IOSRMRouter _router;
    private readonly Dictionary<ushort, Station> _stations;
    private readonly ISpatialGrid _spatialGrid;
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
    /// <param name="stationService">Service for accessing station reservations.</param>
    /// <param name="chargerBufferPercent">Scalar for SoC difference.</param>
    /// <param name="maxConcurrency">The max amount of cores to use.</param>
    public FindCandidateStationService(
        IOSRMRouter router,
        Dictionary<ushort, Station> stations,
        ISpatialGrid spatialGrid,
        StationService stationService,
        float chargerBufferPercent,
        int maxConcurrency = 4)
    {
        _router = router;
        _stations = stations;
        _spatialGrid = spatialGrid;
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
            {
                var ex = new SkillissueException($"Expected event of type FindCandidateStations, but got {fcse.GetType().Name}.");
                Log.Error(ex, "Expected event of type FindCandidateStations, but got {EventType}.", fcse.GetType().Name);
                throw ex;
            }

            var tcs = new TaskCompletionSource<Dictionary<ushort, DurToStationAndDest>>(TaskCreationOptions.RunContinuationsAsynchronously);

            _evStationPaths[e.EV.Id] = new StationQuery(tcs.Task);

            lock (_queueLock)
            {
                _taskQueue.Enqueue((e, tcs), e.Time);
            }

            _queueSignal.Release();
        };
    }

    private Dictionary<ushort, DurToStationAndDest> ComputeCandidates(FindCandidateStations e, double PathdeviationMultiplier = 1.0)
    {
        var ev = e.EV;
        var pos = ev.Advance(e.Time);

        if (pos is null)
        {
            var ex = new SkillissueException($"EV {ev.Id} has no position at time {e.Time} after advancing. This should not happen.");
            Log.Error(ex, "EV {@EVId} has no position at time {@Time} after advancing. This should not happen.", ev.Id, e.Time);
            throw ex;
        }

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
                (var toStationDuration, var toStationDistance) = (detourResult.ToStation.Durations[i], detourResult.ToStation.Distances[i]);
                (var toDestinationDuration, var toDestinationDistance) = (detourResult.ToDest.Durations[i], detourResult.ToDest.Distances[i]);
                if (!ev.CanReachToStation(toStationDistance / 1000f, ev.Preferences.MinAcceptableCharge))
                    continue;

                if (ev.SoCUsedAfterADistance(toStationDistance / 1000) <= 0)
                    continue;

                if (ev.CheckIfTargetSoCIsLowerThanCurrentSoC(toStationDistance, toDestinationDistance, _chargerBufferPercent))
                    continue;

                refinedCandidateDurations[stationId] = new DurToStationAndDest(toStationDuration, toDestinationDuration, toDestinationDistance, toStationDistance);
            }

            if (refinedCandidateDurations.Count == 0 && _stationService.GetReservationStationId(ev.Id) is ushort && ev.DistanceOnCurrentChargeKm() > pathDeviationMultiplied)
            {
                refinedCandidateDurations = ComputeCandidates(e, PathdeviationMultiplier * 1.25);
            }
            else if (refinedCandidateDurations.Count == 0)
            {
                Log.Warning("No candidate stations found for EV {@EVId} at time {@Time}. Even after expanding search radius, no stations were found along the polyline. Returning empty candidate set.", ev.Id, e.Time, ("EV", ev));
            }
            else
            {
                Log.Verbose("Found {CandidateCount} candidate stations for EV {@EVId} at time {@Time}.", refinedCandidateDurations.Count, ev.Id, e.Time, ("EV", ev));
            }

            return refinedCandidateDurations;
        }

        if (_stationService.GetReservationStationId(ev.Id) is ushort && ev.DistanceOnCurrentChargeKm() > pathDeviationMultiplied)
        {
            return ComputeCandidates(e, PathdeviationMultiplier * 1.25);
        }

        Log.Warning("No candidate stations found for EV {@EVId} at time {@Time}. No stations were found along the polyline. Returning empty candidate set.", ev.Id, e.Time, ("EV", ev));
        return [];
    }

    /// <summary>Gets the pre-computed candidate stations. Awaits result if it's not yet ready.</summary>
    /// <param name="evId">The EV's id.</param>
    /// <returns>The pre-computed candidate stations.</returns>
    /// <exception cref="SkillissueException">If you try and get a cached candidate which was never precomputed.</exception>
    public async Task<Dictionary<ushort, DurToStationAndDest>> GetCandidateStationFromCache(int evId)
    {
        if (!_evStationPaths.TryRemove(evId, out var query))
        {
            var ex = new SkillissueException($"No pre-computed station query found for EV {evId}. Ensure PreComputeCandidateStation is called first.");
            Log.Error(ex, "No pre-computed station query found for EV {@EVId}. Ensure PreComputeCandidateStation is called first.", evId);
            throw ex;
        }

        return await query.Task;
    }
}
