namespace Engine.test.Routing;

using Core.Charging;
using Core.Shared;
using Engine.Routing;
using Core.test.Builders;
using Engine.test.Builders;

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

        var result = router.QueryStationsWithDest(
            _evPosition[0],
            _evPosition[1],
            _evPosition[0],
            _evPosition[1],
            [0]);

        Assert.True(result.TotalDuration(0) < 1f, $"Expected ~0s, got {result.TotalDuration(0):F1}s");
        Assert.True(result.TotalDistance(0) < 1f, $"Expected ~0m, got {result.TotalDistance(0):F1}m");
    }

    [Fact]
    public void QueryStationsWithDest_NearbyStationHasLowerDurationThanFarStation()
    {
        using var router = CreateRouter(_stationNearPosition, _stationFarPosition);

        var result = router.QueryStationsWithDest(
            _evPosition[0],
            _evPosition[1],
            _destPosition[0],
            _destPosition[1],
            [0, 1]);

        Assert.True(
            result.TotalDuration(0) < result.TotalDuration(1),
            $"Nearby station should have lower duration: near={result.TotalDuration(0):F1}s far={result.TotalDuration(1):F1}s");
        Assert.True(
            result.TotalDistance(0) < result.TotalDistance(1),
            $"Nearby station should have lower distance: near={result.TotalDistance(0):F1}m far={result.TotalDistance(1):F1}m");
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

        var result = router.QueryStationsWithDest(
            _evPosition[0],
            _evPosition[1],
            _destPosition[0],
            _destPosition[1],
            [0]); // index 0 = _stationNearPosition

        Assert.True(
            Math.Abs(result.ToStation.Durations[0] - evToStationRes.Duration) < 1f,
            $"ToStation={result.ToStation.Durations[0]:F1}s Route={evToStationRes.Duration:F1}s");
        Assert.True(
            Math.Abs(result.ToDest.Durations[0] - stationToDestRes.Duration) < 1f,
            $"ToDest={result.ToDest.Durations[0]:F1}s Route={stationToDestRes.Duration:F1}s");
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
        var withStopsRes = router.QueryDestinationWithStop(
            _evPosition[0], _evPosition[1], stationLon, stationLat, _destPosition[0], _destPosition[1]);
        var tableRes = router.QueryStationsWithDest(
            _evPosition[0], _evPosition[1], _destPosition[0], _destPosition[1], [0]);

        // Values are baselines from OSRM public API
        Assert.Equal(272000f, singleRes.Duration, 5000f);
        Assert.Equal(477000f, singleResDest.Duration, 5000f);
        Assert.Equal(274000f, singleResStationDest.Duration, 5000f);

        // WithStops != sum of independent legs. OSRM waypoint routing takes a different path.
        Assert.Equal(550900f, withStopsRes.Duration, 5000f);
        Assert.Equal(550900f, tableRes.ToStation.Durations[0] + tableRes.ToDest.Durations[0], 5000f);
    }

    [Fact]
    public void QueryStationsWithDest_TableVsRoute_PaddingAnalysis()
    {
        (double evLon, double evLat, double stationLon, double stationLat, double destLon, double destLat)[] routes =
        [
            (12.567938, 55.675942, 12.347044, 55.650864, 10.383505, 55.403777),
            (12.491299, 55.782812, 12.567938, 55.675942, 12.347044, 55.650864),
            (10.383505, 55.403777, 10.463423, 55.369409, 10.037054, 56.044979),
            (10.204106, 56.162828, 10.037054, 56.044979, 9.9187,    57.048956),
            (9.9187,    57.048956, 9.850651,  57.119919, 9.535302,  55.711498),
            (9.535302,  55.711498, 9.472454,  55.490394, 8.452401,  55.476211),
            (8.452401,  55.476211, 8.790476,  55.569282, 9.535302,  55.711498),
            (8.790476,  55.569282, 8.452401,  55.476211, 9.472454,  55.490394),
            (10.204106, 56.162828, 10.463423, 55.369409, 10.383505, 55.403777),
            (12.567938, 55.675942, 10.204106, 56.162828, 9.9187,    57.048956),
            (10.383505, 55.403777, 12.347044, 55.650864, 12.567938, 55.675942),
            (9.472454,  55.490394, 8.790476,  55.569282, 8.452401,  55.476211),
            (10.037054, 56.044979, 10.204106, 56.162828, 9.9187,    57.048956),
            (9.850651,  57.119919, 10.037054, 56.044979, 10.204106, 56.162828),
            (12.347044, 55.650864, 12.491299, 55.782812, 12.567938, 55.675942),
            (9.504537,  56.29718,  9.064745,  56.391183, 10.204106, 56.162828),
            (10.55102,  57.728976, 9.850651,  57.119919, 9.9187,    57.048956),
            (9.763011,  55.506733, 9.472454,  55.490394, 8.452401,  55.476211),
            (11.547389, 55.405734, 12.080299, 55.641456, 12.567938, 55.675942),
            (12.080299, 55.641456, 12.347044, 55.650864, 12.567938, 55.675942),
            (9.756713,  55.904975, 9.064745,  56.391183, 9.504537,  56.29718 ),
            (8.62467,   55.71023,  8.452401,  55.476211, 9.472454,  55.490394),
            (10.215853, 56.462081, 9.504537,  56.29718,  9.9187,    57.048956),
            (9.661421,  55.47525,  9.472454,  55.490394, 9.535302,  55.711498),
            (9.060023,  55.492617, 8.790476,  55.569282, 8.452401,  55.476211),
            (11.793087, 55.469985, 11.547389, 55.405734, 12.080299, 55.641456),
            (10.224423, 56.449652, 10.215853, 56.462081, 9.504537,  56.29718 ),
            (9.461724,  55.248464, 9.661421,  55.47525,  9.472454,  55.490394),
            (9.064745,  56.391183, 9.504537,  56.29718,  9.756713,  55.904975),
            (10.55102,  57.728976, 9.9187,    57.048956, 12.567938, 55.675942),
        ];

        var stationPositions = routes.Select(r => new Position(r.stationLon, r.stationLat)).ToArray();
        using var router = CreateRouter(stationPositions);

        for (var i = 0; i < routes.Length; i++)
        {
            var (evLon, evLat, stationLon, stationLat, destLon, destLat) = routes[i];

            var tableResult = router.QueryStationsWithDest(evLon, evLat, destLon, destLat, [(ushort)i]);
            var routeResult = router.QueryDestinationWithStop(evLon, evLat, stationLon, stationLat, destLon, destLat, (ushort)i);

            var tableDuration = tableResult.TotalDuration(0);
            var tableDist = tableResult.TotalDistance(0);
            var routeDuration = routeResult.Duration;
            var routeDist = routeResult.Distance;

            Assert.True(tableDuration > 0 && routeDuration > 0, $"Route {i + 1}: unroutable (tableDur={tableDuration}, routeDur={routeDuration})");

            var durationRatio = tableDuration / routeDuration;
            var distanceRatio = tableDist / routeDist;

            Assert.True(durationRatio is >= 0.98f and <= 1.02f, $"Route {i + 1}: duration ratio {durationRatio:F4} out of range");
            Assert.True(distanceRatio is >= 0.98f and <= 1.02f, $"Route {i + 1}: distance ratio {distanceRatio:F4} out of range");
        }
    }

    [Fact]
    public void QueryStationsWithDest_TableVsRoute_RealJourneyPaddingAnalysis()
    {
        var random = new Random(42);
        var stations = EngineTestData.AllStations;
        var router = EngineTestData.OSRMRouter;
        var sampler = EngineTestData.JourneySamplerProvider(wetEnabled: true).Current;
        var stationList = stations.Values.ToArray();
        const int iterations = 1000;
        var durationRatios = new List<float>(iterations);
        var distanceRatios = new List<float>(iterations);
        var skipped = 0;

        for (var i = 0; i < iterations; i++)
        {
            var (source, destination) = sampler.SampleSourceToDest(random);
            var ev = source;
            var dest = destination;
            var station = stationList[random.Next(stationList.Length)];

            try
            {
                var tableResult = router.QueryStationsWithDest(
                    ev.Longitude, ev.Latitude,
                    dest.Longitude, dest.Latitude,
                    [station.Id]);
                var routeResult = router.QueryDestinationWithStop(
                    ev.Longitude, ev.Latitude,
                    station.Position.Longitude, station.Position.Latitude,
                    dest.Longitude, dest.Latitude,
                    station.Id);

                var tableDuration = tableResult.TotalDuration(0);
                var tableDist = tableResult.TotalDistance(0);
                var routeDuration = routeResult.Duration;
                var routeDist = routeResult.Distance;
                durationRatios.Add(tableDuration / routeDuration);
                distanceRatios.Add(tableDist / routeDist);
            }
            catch (ArgumentException)
            {
                skipped++;
                continue;
            }
        }

        Assert.True(skipped <= iterations * 0.02, $"Too many unroutable samples: {skipped}/{iterations}");

        Assert.True(durationRatios.Average() is >= 0.99f and <= 1.01f, $"Duration avg ratio {durationRatios.Average():F4} out of range");
        Assert.True(distanceRatios.Average() is >= 0.99f and <= 1.01f, $"Distance avg ratio {distanceRatios.Average():F4} out of range");
        Assert.True(durationRatios.Max() <= 1.20f, $"Duration max ratio {durationRatios.Max():F4} exceeds 1.20");
        Assert.True(distanceRatios.Max() <= 1.20f, $"Distance max ratio {distanceRatios.Max():F4} exceeds 1.20");
    }

    [Fact]
    public void QueryStationsWithDest_EmptyIndices_ReturnsEmptyResult()
    {
        using var router = CreateRouter(_stationNearPosition);
        var result = router.QueryStationsWithDest(_evPosition[0], _evPosition[1], _destPosition[0], _destPosition[1], []);

        Assert.Empty(result.ToStation.Durations);
        Assert.Empty(result.ToStation.Distances);
        Assert.Empty(result.ToDest.Durations);
        Assert.Empty(result.ToDest.Distances);
    }

    [Fact]
    public void QueryStationsWithDest_IndexOrderDeterminesResultOrder()
    {
        using var router = CreateRouter(_stationNearPosition, _stationFarPosition);

        var forward = router.QueryStationsWithDest(
            _evPosition[0], _evPosition[1], _destPosition[0], _destPosition[1], [0, 1]);

        var reversed = router.QueryStationsWithDest(
            _evPosition[0], _evPosition[1], _destPosition[0], _destPosition[1], [1, 0]);

        Assert.Equal(forward.TotalDuration(0), reversed.TotalDuration(1), 0.1f);
        Assert.Equal(forward.TotalDuration(1), reversed.TotalDuration(0), 0.1f);
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
            Assert.Equal(queries[i].expectedDur, parallelResults[i], 0.1f);
    }

    [Fact]
    public async Task QueryStationsWithDest_IsThreadSafe_WithConcurrentQueries()
    {
        using var router = CreateRouter(_stationNearPosition, _stationFarPosition);
        const int numTasks = 1000;

        var queries = Enumerable.Range(0, numTasks)
            .Select(i =>
            {
                var offset = i % 10 * 0.001;
                return (_evPosition[0] + offset, _evPosition[1] + offset);
            }).ToArray();

        var expected = queries
            .Select(q => router.QueryStationsWithDest(q.Item1, q.Item2, _destPosition[0], _destPosition[1], [0, 1]).TotalDuration(0))
            .ToArray();

        var parallelResults = new float[numTasks];
        await Task.WhenAll(Enumerable.Range(0, numTasks).Select(i => Task.Run(() =>
        {
            var (evLon, evLat) = queries[i];
            parallelResults[i] = router.QueryStationsWithDest(
                evLon, evLat, _destPosition[0], _destPosition[1], [0, 1]).TotalDuration(0);
        })));

        for (var i = 0; i < numTasks; i++)
            Assert.Equal(expected[i], parallelResults[i], 0.1f);
    }

    [Fact]
    public async Task QueryPointsToPoints_IsThreadSafe_WithConcurrentQueries()
    {
        using var router = CreateRouter(_stationNearPosition, _stationFarPosition);
        var numTasks = 1000;

        var queries = new (double evLon, double evLat, float expectedDuration)[numTasks];
        for (var i = 0; i < numTasks; i++)
        {
            var offset = i % 10 * 0.001;
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
