namespace Engine.test.Routing;

using Core.Charging;
using Core.Shared;
using Engine.Routing;
using Core.test.Builders;

// If this test ever fails report it. We should have been fixed but just in case.
public class OSRMRouterTests
{
    private static readonly double[] _evPosition = [10.2039, 56.1629];
    private static readonly double[] _destPosition = [10.1572, 56.1496];
    private static readonly Position _stationNearPosition = new(10.1900, 56.1550);
    private static readonly Position _stationFarPosition = new(10.2100, 56.1700);

    private readonly string _osrmPath;

    private OSRMRouter CreateRouter(params Position[] positions)
    {
        List<Station> stations = [.. positions.Select((pos, i) =>
            CoreTestData.Station(
                id: (ushort)(i + 1),
                pos: pos,
                energyPrices: CoreTestData.EnergyPrices))];
        var router = new OSRMRouter(new FileInfo(_osrmPath), stations);
        return router;
    }

    public OSRMRouterTests()
    {
        _osrmPath = AppContext.GetData("OsrmDataPath") as string
            ?? throw new InvalidOperationException("OsrmDataPath not set in project.");
    }

    [Fact]
    public void QueryStationsWithDest_SamePointForAll_ReturnZero()
    {
        using var router = CreateRouter(new Position(_evPosition[0], _evPosition[1]));

        var (durations, distances) = router.QueryStationsWithDest(
            _evPosition[0],
            _evPosition[1],
            _evPosition[0],
            _evPosition[1],
            [0]).ToTotals();

        Assert.True(durations[0] < 1f, $"Expected ~0s, got {durations[0]:F1}s");
        Assert.True(distances[0] < 1f, $"Expected ~0m, got {distances[0]:F1}m");
    }

    [Fact]
    public void QueryStationsWithDest_NearbyStationHasLowerDurationThanFarStation()
    {
        using var router = CreateRouter(_stationNearPosition, _stationFarPosition);

        var (durations, distances) = router.QueryStationsWithDest(
            _evPosition[0],
            _evPosition[1],
            _destPosition[0],
            _destPosition[1],
            [0, 1]).ToTotals();

        Assert.True(
            durations[0] < durations[1],
            $"Nearby station should have lower duration: near={durations[0]:F1}s far={durations[1]:F1}s");
        Assert.True(
            distances[0] < distances[1],
            $"Nearby station should have lower distance: near={distances[0]:F1}m far={distances[1]:F1}m");
    }

    [Fact]
    public void QueryStationsWithDest_BothLegsAreCorrect()
    {
        using var router = CreateRouter(_stationNearPosition, _stationFarPosition);

        var evToStationRes = router.QuerySingleDestination(
            _evPosition[0],
            _evPosition[1],
            _stationNearPosition.Longitude,
            _stationNearPosition.Latitude);

        var stationToDestRes = router.QuerySingleDestination(
            _stationNearPosition.Longitude,
            _stationNearPosition.Latitude,
            _destPosition[0],
            _destPosition[1]);

        var (tableDurations, _) = router.QueryStationsWithDest(
            _evPosition[0],
            _evPosition[1],
            _destPosition[0],
            _destPosition[1],
            [0]).ToTotals(); // index 0 = _stationNearPosition

        var routeSum = evToStationRes.Duration + stationToDestRes.Duration;
        Assert.True(
            Math.Abs(tableDurations[0] - routeSum) < 1f,
            $"Table={tableDurations[0]:F1}s RouteSum={routeSum:F1}s — likely wrong leg wired");
    }

    [Fact]
    public void AllOSRMQueryFunctions_ReturnsTheSameResult()
    {
        // station is directly on route so shouldn't impact routing
        var stationOnRoute = new Position(10.182335, 56.156305);
        var router = CreateRouter([_stationNearPosition, stationOnRoute, _stationFarPosition]);

        var (duration1, _) = router.QueryPointsToPoints(_evPosition, _destPosition);
        var routeSegment2 = router.QuerySingleDestination(_evPosition[0], _evPosition[1], _destPosition[0], _destPosition[1]);
        var routeSegment3 = router.QueryDestinationWithStop(_evPosition[0], _evPosition[1], stationOnRoute.Longitude, stationOnRoute.Latitude, _destPosition[0], _destPosition[1]);

        Assert.Equal(481500, duration1[0]);
        Assert.Equal(481500, routeSegment2.Duration);
        Assert.Equal(481500, routeSegment3.Duration);
    }

    [Fact]
    public void OSRM_CompareAllMethods_Durations()
    {
        using var router = CreateRouter(_stationNearPosition);
        var stationLon = _stationNearPosition.Longitude;
        var stationLat = _stationNearPosition.Latitude;

        var singleRes = router.QuerySingleDestination(
            _evPosition[0], _evPosition[1], stationLon, stationLat);

        var singleResDest = router.QuerySingleDestination(
            _evPosition[0], _evPosition[1], _destPosition[0], _destPosition[1]);

        var singleResStationDest = router.QuerySingleDestination(
            stationLon, stationLat, _destPosition[0], _destPosition[1]);

        var p2pRes = router.QueryPointsToPoints(
            [_evPosition[0], _evPosition[1]],
            [stationLon, stationLat]);

        var queryDestRes = router.QuerySingleDestination(
            _evPosition[0], _evPosition[1], stationLon, stationLat);

        var queryDestRes2 = router.QuerySingleDestination(
            stationLon, stationLat, _destPosition[0], _destPosition[1]);

        var withStopsRes = router.QueryDestinationWithStop(
            _evPosition[0], _evPosition[1], stationLon, stationLat, _destPosition[0], _destPosition[1]);

        // Values are baselines from OSRM public API
        Assert.Equal(272000f, singleRes.Duration, 5000f);          // EV->Station
        Assert.Equal(477000f, singleResDest.Duration, 5000f);      // EV->Dest
        Assert.Equal(274000f, singleResStationDest.Duration, 5000f); // Station->Dest

        // WithStops != sum of independent legs (550s) OSRM waypoint routing takes a different path. Consistent with OSRM public API (587s).
        Assert.Equal(587000f, withStopsRes.Duration, 5000f);
    }

    [Fact]
    public void QueryStationsWithDest_EmptyIndices_ReturnsEmptyResult()
    {
        using var router = CreateRouter(_stationNearPosition);
        var result = router.QueryStationsWithDest(_evPosition[0], _evPosition[1], _destPosition[0], _destPosition[1], []).ToTotals();

        Assert.Empty(result.Durations);
        Assert.Empty(result.Distances);
    }

    [Fact]
    public void QueryStationsWithDest_IndexOrderDeterminesResultOrder()
    {
        using var router = CreateRouter(_stationNearPosition, _stationFarPosition);

        var (durationsForward, _) = router.QueryStationsWithDest(
            _evPosition[0], _evPosition[1], _destPosition[0], _destPosition[1], [0, 1]).ToTotals();

        var reversed = router.QueryStationsWithDest(
            _evPosition[0], _evPosition[1], _destPosition[0], _destPosition[1], [1, 0]).ToTotals();

        Assert.Equal(durationsForward[0], reversed.Durations[1], 0.1f);
        Assert.Equal(durationsForward[1], reversed.Durations[0], 0.1f);
    }

    [Fact]
    public async Task OSRMRouter_IsThreadSafe_WithConcurrentUniqueQueries()
    {
        using var router = CreateRouter();
        var numTasks = 1000;

        var queries = new (double startLon, double startLat, double endLon, double endLat, float expectedDur)[numTasks];

        for (var i = 0; i < numTasks; i++)
        {
            var offset = i * 0.001;
            var targetLon = _destPosition[0] + offset;
            var targetLat = _destPosition[1] + offset;

            var seqResult = router.QuerySingleDestination(_evPosition[0], _evPosition[1], targetLon, targetLat);

            queries[i] = (_evPosition[0], _evPosition[1], targetLon, targetLat, seqResult.Duration);
        }

        var parallelResults = new float[numTasks];
        var tasks = Enumerable.Range(0, numTasks).Select(i => Task.Run(() =>
        {
            var (startLon, startLat, endLon, endLat, expectedDur) = queries[i];
            var res = router.QuerySingleDestination(startLon, startLat, endLon, endLat);
            parallelResults[i] = res.Duration;
        }));

        await Task.WhenAll(tasks);

        for (var i = 0; i < numTasks; i++)
        {
            Assert.Equal(queries[i].expectedDur, parallelResults[i], 0.1f);
        }
    }

    [Fact]
    public async Task QueryStationsWithDest_IsThreadSafe_WithConcurrentQueries()
    {
        using var router = CreateRouter(_stationNearPosition, _stationFarPosition);
        var numTasks = 1000;

        var queries = new (double evLon, double evLat, float expectedDuration)[numTasks];
        for (var i = 0; i < numTasks; i++)
        {
            var offset = i * 0.001;
            var (durations, _) = router.QueryStationsWithDest(
                _evPosition[0] + offset, _evPosition[1] + offset, _destPosition[0], _destPosition[1], [0, 1]).ToTotals();
            queries[i] = (_evPosition[0] + offset, _evPosition[1] + offset, durations[0]);
        }

        var parallelResults = new float[numTasks];
        await Task.WhenAll(Enumerable.Range(0, numTasks).Select(i => Task.Run(() =>
        {
            var (evLon, evLat, _) = queries[i];
            var (durations, _) = router.QueryStationsWithDest(
                evLon, evLat, _destPosition[0], _destPosition[1], [0, 1]).ToTotals();
            parallelResults[i] = durations[0];
        })));

        for (var i = 0; i < numTasks; i++)
            Assert.Equal(queries[i].expectedDuration, parallelResults[i], 0.1f);
    }

    [Fact]
    public async Task QueryPointsToPoints_IsThreadSafe_WithConcurrentQueries()
    {
        using var router = CreateRouter(_stationNearPosition, _stationFarPosition);
        var numTasks = 1000;

        var queries = new (double evLon, double evLat, float expectedDuration)[numTasks];
        for (var i = 0; i < numTasks; i++)
        {
            var offset = i * 0.001;
            var (durations, _) = router.QueryPointsToPoints(
                [_evPosition[0] + offset, _evPosition[1] + offset],
                [_stationNearPosition.Longitude, _stationNearPosition.Latitude]);
            queries[i] = (_evPosition[0] + offset, _evPosition[1] + offset, durations[0]);
        }

        var parallelResults = new float[numTasks];
        await Task.WhenAll(Enumerable.Range(0, numTasks).Select(i => Task.Run(() =>
        {
            var (evLon, evLat, _) = queries[i];
            var (durations, _) = router.QueryPointsToPoints(
                [evLon, evLat],
                [_stationNearPosition.Longitude, _stationNearPosition.Latitude]);
            parallelResults[i] = durations[0];
        })));

        for (var i = 0; i < numTasks; i++)
            Assert.Equal(queries[i].expectedDuration, parallelResults[i], 0.1f);
    }
}
