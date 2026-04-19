namespace Engine.Events.Middleware;

using Core.Charging;
using Core.Shared;
using Core.Vehicles;
using Engine.Grid;
using Engine.Routing;
using Engine.Services;
using Engine.Utils;
using Engine.Vehicles;

/// <summary>Service responsible for pre-computing the candidate stations for an EV and caching the results for later retrieval.</summary>
/// <param name="router">Router used to compute paths from the EV's position to candidate stations and the destination.</param>
/// <param name="stations">Stations to choose from.</param>
/// <param name="spatialGrid">Used for pruning of <paramref name="stations"/>.</param>
/// <param name="evStore">Gives access to EV's.</param>
/// <param name="stationService">Gives access to station information.</param>
/// <param name="settings">Engine settings.</param>
public class FindCandidateStationService(
    IOSRMRouter router,
    Dictionary<ushort, Station> stations,
    ISpatialGrid spatialGrid,
    EVStore evStore,
    StationService stationService,
    Engine.Init.EngineSettings settings) : IFindCandidateStationService
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
        var pos = ev.Advance(e.Time);

        var pathDeviationMultiplied = ev.Preferences.MaxPathDeviation * PathdeviationMultiplier;

        var filteredStationIds = spatialGrid.GetStationsAlongPolyline(
            ev.Journey.Current.Waypoints,
            pathDeviationMultiplied);

        var reachableStationIds = ReachableStations.FindReachableStations(
            ev.Journey.Current.Waypoints,
            ev,
            stations,
            filteredStationIds,
            pathDeviationMultiplied).ToArray();

        var destination = ev.Journey.Current.Waypoints.Last();
        var detourLegs = router.QueryStationsWithDest(
            pos.Longitude,
            pos.Latitude,
            destination.Longitude,
            destination.Latitude,
            reachableStationIds);

        var candidates = FilterCandidates(ref ev, detourLegs, reachableStationIds, e.Time);

        if (candidates.Count == 0 && stationService.GetReservationStationId(e.EVId) is ushort && ev.DistanceOnCurrentChargeKm() > pathDeviationMultiplied)
        {
            candidates = ComputeCandidates(e, PathdeviationMultiplier * 1.25);
        }

        return candidates;
    }

    private Dictionary<ushort, float> FilterCandidates(ref EV ev, RoutingResultLegs detourLegs, ushort[] reachableStationIds, Time currentTime)
    {
        var usableRangeKm = (ev.Battery.CurrentChargeKWh - (ev.Battery.MaxCapacityKWh * ev.Preferences.MinAcceptableCharge)) / (ev.ConsumptionWhPerKm / 1000f);
        if (usableRangeKm >= ev.Journey.Current.DistanceKm)
            return [];

        var result = new Dictionary<ushort, float>();
        var journeyEnd = ev.Journey.Current.Departure + ev.Journey.Current.Duration;

        for (var i = 0; i < reachableStationIds.Length; i++)
        {
            var toStation = detourLegs.SrcToStation[i];
            var toDest = detourLegs.StationToDest[i];

            var totalDistance = toStation.Distance + toDest.Distance;
            if (totalDistance < 0 || float.IsNaN(totalDistance))
                continue;

            if (!ev.CanReachViaDetour(totalDistance / 1000f, ev.Journey.Current.DistanceKm, ev.Preferences.MinAcceptableCharge))
                continue;

            var isEnRoute = ev.Journey.Current.NextStopId == reachableStationIds[i];

            var socAtStation = isEnRoute
                ? ev.EstimateSoCAtNextStop()
                : (ev.Battery.CurrentChargeKWh - ev.EnergyForDistanceKWh(toStation.Distance / 1000f)) / ev.Battery.MaxCapacityKWh;

            var etaToStation = currentTime + (uint)toStation.Duration;
            var lateArrival = etaToStation > ev.Journey.Current.EtaToNextStop || etaToStation > journeyEnd;
            var desiredSoC = lateArrival ? ev.Preferences.MinAcceptableCharge : ev.CalcDesiredSoC(etaToStation);

            if (socAtStation >= desiredSoC * settings.ChargeBufferPercent)
                continue;

            result[reachableStationIds[i]] = toStation.Duration + toDest.Duration;
        }

        return result;
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
