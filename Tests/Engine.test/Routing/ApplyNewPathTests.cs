namespace Engine.test.Routing;

using Core.Shared;
using Engine.Routing;
using Engine.test.Builders;

public class ApplyNewPathToEVTests()
{
    [Fact]
    public void ApplyNewPath_WhenApplyingSamePath_IsSame()
    {
        var journey = TestData.Journey(
            waypoints: null,
            departure: new Time(0),
            originalDuration: 60U);

        var stationPosition = new Position(2, 2);
        var dummyRoute = new List<Position>([new(0, 0), stationPosition, new(5, 5)]);
        journey.UpdateRoute(dummyRoute, stationPosition, departure: new Time(0), duration: new Time(60), 0);
        journey.UpdateRoute(dummyRoute, stationPosition, departure: new Time(20), duration: new Time(40), 0);

        Assert.Equal(0U, (uint)journey.Current.PathDeviation);
    }

    [Fact]
    public void ApplyNewPathToEV_ConvertsAndRoundsDurationCorrectly()
    {
        var ev = TestData.EV(waypoints: [
            new (0, 0),
            new (10, 10)
        ]);
        var station = TestData.Station(1, new(5, 5));
        var fakeRouter = new FakeDestinationRouter
        {
            ReturnedDuration = 150.0f, // 2.5 minutes, should round to 2
        };
        var applyNewPath = new EVDetourPlanner(fakeRouter);
        var currentTime = new Time(10);

        applyNewPath.Update(ref ev, station, currentTime);

        Assert.Equal(150.0f, (uint)ev.Journey.Current.Duration);
        Assert.Equal(10U, (uint)ev.Journey.Current.Departure);
    }

    [Fact]
    public void UpdateRoute_AccumulatesPathDeviationCorrectly()
    {
        var journey = TestData.Journey(
            waypoints: null,
            departure: new Time(100),
            originalDuration: 50U);

        var dummyRoute = new List<Position>([]);
        var dummyPosition = new Position(0, 0);
        var dummyJourneyLengthkm = 10;

        journey.UpdateRoute(dummyRoute, dummyPosition, departure: new Time(100), duration: new Time(60), dummyJourneyLengthkm);
        Assert.Equal(10U, (uint)journey.Current.PathDeviation);

        journey.UpdateRoute(dummyRoute, dummyPosition, departure: new Time(110), duration: new Time(40), dummyJourneyLengthkm);
        Assert.Equal(0U, (uint)journey.Current.PathDeviation);
    }

    [Fact]
    public void ApplyNewPathToEV_ThrowsArgumentException_WhenTimeIsOutOfBounds()
    {
        var ev = TestData.EV(
            waypoints: [new Position(0, 0), new Position(10, 10)],
            departureTime: new Time(100),
            originalDuration: 50U);

        var fakeRouter = new FakeDestinationRouter();
        var applyNewPath = new EVDetourPlanner(fakeRouter);
        var station = TestData.Station(1, new Position(5, 5));

        Assert.Throws<InvalidOperationException>(() =>
            applyNewPath.Update(ref ev, station, new Time(99)));

        const uint outsideApproxTolerance = 31;
        Assert.Throws<ArgumentException>(() =>
            applyNewPath.Update(ref ev, station, new Time(150 + outsideApproxTolerance)));
    }

    public class FakeDestinationRouter : IDestinationRouter
    {
        private const float _tenMinutesInSeconds = 600.0f;

        public float ReturnedDuration { get; set; } = _tenMinutesInSeconds;

        public float ReturnedDistance { get; set; } = 0;

        public string ReturnedPolyline { get; set; } = "_p~iF~ps|U_ulLnnqC_mqNvxq`@";

        public RouteSegment QueryDestinationWithStop(
            double evLon, double evLat, double stationLon, double stationLat, double destLon, double destLat, ushort index) => new(ReturnedDuration, ReturnedDistance, ReturnedPolyline);
    }
}
