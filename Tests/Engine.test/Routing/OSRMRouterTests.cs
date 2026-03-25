namespace Engine.test.Routing;

using Core.Charging;
using Core.Shared;
using Engine.Routing;

// If this test ever fails report it. We should have been fixed but just in case.
public class OSRMRouterTests
{
    private static readonly double[] _evPosition = [10.2039, 56.1629];
    private static readonly double[] _destPosition = [10.1572, 56.1496];
    private static readonly Position _stationNearPosition = new(10.1900, 56.1550);
    private static readonly Position _stationFarPosition = new(10.2100, 56.1700);

    private readonly string _osrmPath;
    private readonly EnergyPrices _energyPrices;

    private OSRMRouter CreateRouter(params Position[] positions)
    {
        var router = new OSRMRouter(new FileInfo(_osrmPath));
        router.InitStations([.. positions.Select((pos, i) => new Station(
            id: (ushort)(i + 1),
            name: string.Empty,
            address: string.Empty,
            position: pos,
            chargers: [],
            random: new Random(1),
            energyPrices: _energyPrices))]);
        return router;
    }

    public OSRMRouterTests()
    {
        _osrmPath = AppContext.GetData("OsrmDataPath") as string
            ?? throw new InvalidOperationException("OsrmDataPath not set in project.");

        var csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "energy_prices.csv");
        _energyPrices = new EnergyPrices(new FileInfo(csvPath));
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
    public void QueryStationsWithDest_EvToStationLeg_MatchesQueryStations()
    {
        using var router = CreateRouter(_stationNearPosition, _stationFarPosition);

        var (queryStationsDurations, queryStationsDistances) = router.QueryStations(
            _evPosition[0], _evPosition[1], [0]);

        var (withDestDurations, withDestDistances) = router.QueryStationsWithDest(
            _evPosition[0],
            _evPosition[1],
            _stationNearPosition.Longitude,
            _stationNearPosition.Latitude,
            [0]);

        Assert.True(
            Math.Abs(queryStationsDurations[0] - withDestDurations[0]) < 10f,
            $"EV→station leg mismatch: QueryStations={queryStationsDurations[0]:F1}s QueryStationsWithDest={withDestDurations[0]:F1}s");

        Assert.True(
            Math.Abs(queryStationsDistances[0] - withDestDistances[0]) < 50f,
            $"EV→station leg mismatch: QueryStations={queryStationsDistances[0]:F1}m QueryStationsWithDest={withDestDistances[0]:F1}m");
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

        var (evToStation, _) = router.QuerySingleDestination(
            _evPosition[0],
            _evPosition[1],
            _stationNearPosition.Longitude,
            _stationNearPosition.Latitude);

        var (stationToDest, _) = router.QuerySingleDestination(
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

        var routeSum = evToStation + stationToDest;
        Assert.True(
            Math.Abs(tableDurations[0] - routeSum) < 1f,
            $"Table={tableDurations[0]:F1}s RouteSum={routeSum:F1}s — likely wrong leg wired");
    }
}
