namespace Engine.test.Routing;

using Core.Shared;
using Engine.Routing;
using Engine.test.Builders;
using Core.test.Builders;

public class ApplyNewPathToEVTests()
{
    [Fact]
    public void ApplyNewPath_WhenApplyingSamePath_IsSame()
    {
        var journey = CoreTestData.Journey(
            waypoints: null,
            departure: new Time(0),
            originalDuration: 60000U);

        var stationPosition = new Position(2, 2);
        var dummyRoute = new List<Position> { new(0, 0), stationPosition, new(5, 5) };

        journey.UpdateRoute(dummyRoute, stationPosition, departure: new Time(0), duration: new Time(60000), newDistanceKm: 10);
        journey.UpdateRoute(dummyRoute, stationPosition, departure: new Time(20000), duration: new Time(40000), newDistanceKm: 10);

        Assert.Equal(0U, (uint)journey.Current.PathDeviation);
    }

    [Fact]
    public void ApplyNewPathToEV_ConvertsAndRoundsDurationCorrectly()
    {
        var ev = CoreTestData.EV(waypoints:
        [
            new(0, 0),
            new(10, 10),
        ]);

        var station = CoreTestData.Station(1, new(10, 10));
        var fakeRouter = new FakeDestinationRouter
        {
            ReturnedDuration = 150f,
            ReturnedPolyline = "??_qo]_qo]_qo]_qo]",
        };

        var applyNewPath = new EVDetourPlanner(fakeRouter);
        var currentTime = new Time(10);

        applyNewPath.Update(ref ev, station, currentTime);

        Assert.Equal(150U, (uint)ev.Journey.Current.Duration);
        Assert.Equal(10U, (uint)ev.Journey.Current.Departure);
    }

    [Fact]
    public void UpdateRoute_AccumulatesPathDeviationCorrectly()
    {
        var journey = CoreTestData.Journey(
            waypoints:
            [
                new(0, 0),
                new(10, 10),
            ],
            departure: new Time(100000),
            originalDuration: 50000U);

        var stationPosition = new Position(5, 5);
        var dummyRoute = new List<Position>
        {
            new(0, 0),
            stationPosition,
            new(10, 10),
        };

        const float dummyJourneyLengthKm = 10;

        journey.UpdateRoute(dummyRoute, stationPosition, departure: new Time(100000), duration: new Time(60000), dummyJourneyLengthKm);
        Assert.Equal(10000U, (uint)journey.Current.PathDeviation);

        journey.UpdateRoute(dummyRoute, stationPosition, departure: new Time(110000), duration: new Time(40000), dummyJourneyLengthKm);
        Assert.Equal(0U, (uint)journey.Current.PathDeviation);
    }

    [Fact]
    public void ApplyNewPathToEV_ThrowsArgumentException_WhenTimeIsOutOfBounds()
    {
        var ev = CoreTestData.EV(
            waypoints:
            [
                new(0, 0),
                new(10, 10),
            ],
            departureTime: new Time(100000),
            originalDuration: 50000U);

        var fakeRouter = new FakeDestinationRouter();
        var applyNewPath = new EVDetourPlanner(fakeRouter);
        var station = CoreTestData.Station(1, new Position(5, 5));

        Assert.Throws<InvalidOperationException>(() =>
            applyNewPath.Update(ref ev, station, new Time(99000)));

        const uint outsideApproxTolerance = 310000;

        Assert.Throws<ArgumentException>(() =>
            applyNewPath.Update(ref ev, station, new Time(150000 + outsideApproxTolerance)));
    }

    public class FakeDestinationRouter : IDestinationRouter
    {
        private const float _tenMinutesInMilliseconds = 600000.0f;

        public float ReturnedDuration { get; set; } = _tenMinutesInMilliseconds;

        public float ReturnedDistance { get; set; } = 0;

        public string ReturnedPolyline { get; set; } = "_p~iF~ps|U_ulLnnqC_mqNvxq`@";

        public RouteSegment QueryDestinationWithStop(
            double evLon,
            double evLat,
            double stationLon,
            double stationLat,
            double destLon,
            double destLat,
            ushort index) => new(ReturnedDuration, ReturnedDistance, ReturnedPolyline);
    }
}