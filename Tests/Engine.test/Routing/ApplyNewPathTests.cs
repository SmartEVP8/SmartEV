namespace Engine.test.Routing;

using Core.Shared;
using Engine.Routing;
using Engine.test.Builders;

public class ApplyNewPathToEVTests()
{
    [Fact]
    public void ApplyNewPathToEV_PassesCorrectCoordinatesToRouter()
    {
        var ev = TestData.EV(waypoints: [
            new Position(longitude: 0, latitude: 0),
            new Position(longitude: 10, latitude: 10)
        ]);
        var station = TestData.Station(1, new Position(longitude: 5, latitude: 5));
        var fakeRouter = new FakeDestinationRouter();
        var applyNewPath = new ApplyNewPath(fakeRouter);

        applyNewPath.ApplyNewPathToEV(ref ev, station, new Time(0));

        Assert.Equal(0, fakeRouter.ReceivedEvLon);
        Assert.Equal(0, fakeRouter.ReceivedEvLat);
        Assert.Equal(5, fakeRouter.ReceivedStationLon);
        Assert.Equal(5, fakeRouter.ReceivedStationLat);
        Assert.Equal(10, fakeRouter.ReceivedDestLon);
        Assert.Equal(10, fakeRouter.ReceivedDestLat);
        Assert.Equal((ushort)1, fakeRouter.ReceivedIndex);
    }

    [Fact]
    public void ApplyNewPath_WhenApplyingSamePath_IsSame()
    {
        var journey = TestData.Journey(
            waypoints: null,
            departure: new Time(0),
            originalDuration: 60U);

        var stationPosition = new Position(2, 2);
        var dummyRoute = new Paths([new Position(0, 0), stationPosition, new Position(5, 5)]);
        journey.UpdateRoute(dummyRoute, stationPosition, departure: new Time(0), duration: new Time(60), 0);
        journey.UpdateRoute(dummyRoute, stationPosition, departure: new Time(20), duration: new Time(40), 0);

        Assert.Equal(0U, (uint)journey.PathDeviation);
    }

    [Fact]
    public void ApplyNewPathToEV_ConvertsAndRoundsDurationCorrectly()
    {
        var ev = TestData.EV(waypoints: [
            new Position(0, 0),
            new Position(10, 10)
        ]);
        var station = TestData.Station(1, new Position(5, 5));
        var fakeRouter = new FakeDestinationRouter
        {
            ReturnedDuration = 150.0f, // 2.5 minutes, should round to 2
        };
        var applyNewPath = new ApplyNewPath(fakeRouter);
        var currentTime = new Time(10);

        applyNewPath.ApplyNewPathToEV(ref ev, station, currentTime);

        Assert.Equal(150.0f, (uint)ev.Journey.LastUpdatedDuration);
        Assert.Equal(10U, (uint)ev.Journey.LastUpdatedDeparture);
    }

    [Fact]
    public void UpdateRoute_AccumulatesPathDeviationCorrectly()
    {
        var journey = TestData.Journey(
            waypoints: null,
            departure: new Time(100),
            originalDuration: 50U);

        var dummyRoute = new Paths([]);
        var dummyPosition = new Position(0, 0);
        var dummyJourneyLengthkm = 10;

        journey.UpdateRoute(dummyRoute, dummyPosition, departure: new Time(100), duration: new Time(60), dummyJourneyLengthkm);
        Assert.Equal(10U, (uint)journey.PathDeviation);

        journey.UpdateRoute(dummyRoute, dummyPosition, departure: new Time(110), duration: new Time(40), dummyJourneyLengthkm);
        Assert.Equal(0U, (uint)journey.PathDeviation);
    }

    [Fact]
    public void ApplyNewPathToEV_ThrowsArgumentException_WhenTimeIsOutOfBounds()
    {
        var ev = TestData.EV(
            waypoints: [new Position(0, 0), new Position(10, 10)],
            departureTime: new Time(100),
            originalDuration: 50U);

        var fakeRouter = new FakeDestinationRouter();
        var applyNewPath = new ApplyNewPath(fakeRouter);
        var station = TestData.Station(1, new Position(5, 5));

        Assert.Throws<ArgumentException>(() =>
            applyNewPath.ApplyNewPathToEV(ref ev, station, new Time(99)));

        Assert.Throws<ArgumentException>(() =>
            applyNewPath.ApplyNewPathToEV(ref ev, station, new Time(151)));
    }

    public class FakeDestinationRouter : IDestinationRouter
    {
        private const float _tenMinutesInSeconds = 600.0f;

        public float ReturnedDuration { get; set; } = _tenMinutesInSeconds;

        public float ReturnedDistance { get; set; } = 0;

        public string ReturnedPolyline { get; set; } = "_p~iF~ps|U_ulLnnqC_mqNvxq`@";

        public double? ReceivedEvLon { get; private set; }

        public double? ReceivedEvLat { get; private set; }

        public double? ReceivedStationLon { get; private set; }

        public double? ReceivedStationLat { get; private set; }

        public double? ReceivedDestLon { get; private set; }

        public double? ReceivedDestLat { get; private set; }

        public ushort? ReceivedIndex { get; private set; }

        public RouteSegment QueryDestinationWithStop(
            double evLon, double evLat, double stationLon, double stationLat, double destLon, double destLat, ushort index)
        {
            ReceivedEvLon = evLon;
            ReceivedEvLat = evLat;
            ReceivedStationLon = stationLon;
            ReceivedStationLat = stationLat;
            ReceivedDestLon = destLon;
            ReceivedDestLat = destLat;
            ReceivedIndex = index;

            return new RouteSegment(ReturnedDuration, ReturnedDistance, ReturnedPolyline);
        }
    }
}
