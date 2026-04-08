namespace Engine.Events.Middleware;

using Core.Charging;
using Core.GeoMath;
using Core.Shared;
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
    private readonly record struct CandidateSnapshot(
        int EVId,
        Position CurrentPosition,
        List<Position> Waypoints,
        double MaxPathDeviationKm,
        float MinAcceptableCharge,
        float DirectDistanceKm,
        float BatteryStateOfCharge,
        ushort BatteryCapacityKWh,
        ushort ConsumptionWhPerKm);

    private readonly Dictionary<int, Task<Dictionary<ushort, float>>> _evStationPaths = [];

    /// <summary>
    /// Computes the calculation of the path calculations from an EV's position to its relevant stations.
    /// </summary>
    /// <exception cref="SkillissueException">If the passed MiddlewareEvent is not a FindCandidateStations.</exception>
    /// <returns>An action that computes the candidate stations for an EV and caches the results for later retrieval.</returns>
    public Action<IMiddlewareEvent> PreComputeCandidateStation() => fcse =>
    {
        if (fcse is not FindCandidateStations e)
            throw new SkillissueException("Not the correct event type");

        ref var ev = ref evStore.Get(e.EVId);
        var snapshot = new CandidateSnapshot(
            EVId: e.EVId,
            CurrentPosition: ev.Advance(e.Time),
            Waypoints: [.. ev.Journey.Current.Waypoints],
            MaxPathDeviationKm: ev.Preferences.MaxPathDeviation,
            MinAcceptableCharge: ev.Preferences.MinAcceptableCharge,
            DirectDistanceKm: ev.Journey.Current.DistanceKm,
            BatteryStateOfCharge: ev.Battery.StateOfCharge,
            BatteryCapacityKWh: ev.Battery.MaxCapacityKWh,
            ConsumptionWhPerKm: ev.ConsumptionWhPerKm);

        _evStationPaths[e.EVId] = Task.Run(() => ComputeCandidates(snapshot));
    };

    private Dictionary<ushort, float> ComputeCandidates(CandidateSnapshot snapshot)
    {
        var filteredStationIds = spatialGrid.GetStationsAlongPolyline(snapshot.Waypoints, snapshot.MaxPathDeviationKm);
        var reachableStationIds = FindReachableStations(snapshot, filteredStationIds, stations);

        var destination = snapshot.Waypoints.Last();
        var detourResult = router.QueryStationsWithDest(
            snapshot.CurrentPosition.Longitude,
            snapshot.CurrentPosition.Latitude,
            destination.Longitude,
            destination.Latitude,
            reachableStationIds);

        var result = new Dictionary<ushort, float>(reachableStationIds.Length);
        for (var i = 0; i < reachableStationIds.Length; i++)
        {
            var detourDistanceMeters = detourResult.Distances[i];
            if (detourDistanceMeters < 0 || float.IsNaN(detourDistanceMeters))
                continue;
            if (!CanReachViaDetour(snapshot, detourDistanceMeters / 1000f))
                continue;

            result[reachableStationIds[i]] = detourResult.Durations[i];
        }

        return result;
    }

    private static ushort[] FindReachableStations(
        CandidateSnapshot snapshot,
        List<ushort> nearbyStationIds,
        Dictionary<ushort, Station> stations)
    {
        var reachKm = snapshot.BatteryStateOfCharge * snapshot.BatteryCapacityKWh / (snapshot.ConsumptionWhPerKm / 1000f);
        return [.. nearbyStationIds.Where(id =>
        {
            var dist = GeoMath.DistancesThroughPath(snapshot.Waypoints, stations[id].Position, snapshot.MaxPathDeviationKm);
            return dist > -1 && dist <= reachKm;
        })];
    }

    private static bool CanReachViaDetour(CandidateSnapshot snapshot, float detourDistanceKm)
    {
        var inferredStationLegKm = Math.Max(0f, detourDistanceKm - snapshot.DirectDistanceKm);
        var reserveKWh = snapshot.BatteryCapacityKWh * snapshot.MinAcceptableCharge;
        var usableKWh = snapshot.BatteryCapacityKWh * snapshot.BatteryStateOfCharge - reserveKWh;
        var energyNeededKWh = inferredStationLegKm * snapshot.ConsumptionWhPerKm / 1000f;
        return energyNeededKWh <= usableKWh;
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
        return await query;
    }
}