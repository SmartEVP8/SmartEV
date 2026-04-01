namespace Engine.test.Routing;

using Core.Charging;
using Core.Shared;
using Engine.Routing;
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
            TestData.Station(
                id: (ushort)(i + 1),
                pos: pos,
                energyPrices: TestData.EnergyPrices))];
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
            [0]);

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
            [0, 1]);

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
            [0]); // index 0 = _stationNearPosition

        var routeSum = evToStationRes.Duration + stationToDestRes.Duration;
        Assert.True(
            Math.Abs(tableDurations[0] - routeSum) < 1f,
            $"Table={tableDurations[0]:F1}s RouteSum={routeSum:F1}s — likely wrong leg wired");
    }

    [Fact]
    public void AllOSRMQueryFunctions_ReturnsTheSameResult()
    {
        var stationOnRoute = new Position(10.182335, 56.156305);
        var router = CreateRouter([_stationNearPosition, stationOnRoute, _stationFarPosition]);

        var (duration1, _) = router.QueryPointsToPoints(_evPosition, _destPosition);
        var routeSegment2 = router.QuerySingleDestination(_evPosition[0], _evPosition[1], _destPosition[0], _destPosition[1]);
        var routeSegment3 = router.QueryDestinationWithStop(_evPosition[0], _evPosition[1], stationOnRoute.Longitude, stationOnRoute.Latitude, _destPosition[0], _destPosition[1]);

        Assert.Equal(481.5f, duration1[0]);
        Assert.Equal(481.5f, routeSegment2.Duration);
        Assert.Equal(481.5f, routeSegment3.Duration);
    }
}
