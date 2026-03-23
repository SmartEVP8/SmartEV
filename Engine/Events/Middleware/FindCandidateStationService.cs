namespace Engine.Events.Middleware;

using Core.Charging;
using Engine.Grid;
using Engine.Routing;
using Engine.Utils;
using Engine.Vehicles;

public class FindCandidateStationService(
    IOSRMRouter router,
    Dictionary<ushort, Station> stations,
    SpatialGrid spatialGrid,
    EVStore evStore)
{
    private record StationQuery(ushort[] StationIds, Task<(float[] Durations, float[] Distances)> Task);

    private readonly Dictionary<int, StationQuery> _evStationPaths = [];

    /// <summary>
    /// Computes the calculation of the path calculations from an EV's position to its relevant stations.
    /// </summary>
    /// <exception cref="SkillissueException">If the passed MiddlewareEvent is not a FindCandidateStations.</exception>
    /// <returns>😎.</returns>
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

            _evStationPaths[fcse.EVId] = new StationQuery(reachableStationsIds, Task.Run(() => router.QueryStationsWithDest(
                pos.Longitude,
                pos.Latitude,
                dest.Longitude,
                dest.Latitude,
                reachableStationsIds)));
        };
    }

    /// <summary>Gets the pre-computed candidate stations. Awaits result if it's not yet ready.</summary>
    /// <param name="evId">The EV's id.</param>
    /// <returns>The pre-computed candidate stations.</returns>
    public async Task<Dictionary<ushort, float>> ComputeCandidateStationFromCache(int evId)
    {
        var (stationIds, task) = _evStationPaths[evId];
        var (durations, _) = await task;
        return stationIds.Zip(durations).ToDictionary(x => x.First, x => x.Second);
    }
}

