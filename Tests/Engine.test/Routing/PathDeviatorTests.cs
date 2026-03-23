using Core.Routing;
using Core.Shared;
using Engine.Routing;

public class PathDeviatorTests
{
    private class CapturingStubRouter(float detourDuration) : IDestinationRouter
    {
        private readonly float _detourDuration = detourDuration;

        public double[]? CapturedCoords { get; private set; }

        public (float duration, string polyline) QueryDestination(double[] coords)
        {
            CapturedCoords = coords;
            return (_detourDuration, string.Empty);
        }
    }

    [Fact]
    public void CalculateDetourDeviation_DirectJourney_EvaluatesCharger()
    {
        var a = new Position(0.0, 0.0);
        var b = new Position(2.0, 0.0);
        var c = new Position(1.0, 1.0); // candidate charger

        var journey = new Journey(
            departure: new Time(0),
            originalDuration: new Time(1000),
            path: new Paths([a, b]));

        // At t=500, remaining = 500. Detour via C to B takes 700. Deviation = 200.
        var router = new CapturingStubRouter(detourDuration: 700);
        var deviator = new PathDeviator(router);
        var (deviation, _) = deviator.CalculateDetourDeviation(journey, currentTime: new(500), stationPosition: c);

        Assert.Equal(200, deviation);
    }

    [Fact]
    public void CalculateDetourDeviation_StationCancelledEnRoute_EvaluatesReplacementStation()
    {
        var a = new Position(0.0, 0.0);
        var c = new Position(1.0, 0.0); // cancelled station
        var d = new Position(1.0, 1.0); // replacement candidate
        var b = new Position(2.0, 0.0);

        var journey = new Journey(
            departure: new Time(0),
            originalDuration: new Time(1000),
            path: new Paths([a, c, b]));

        // At t=250, remaining = 750. Detour via D to B takes 900. Deviation = 150.
        var router = new CapturingStubRouter(detourDuration: 900);
        var deviator = new PathDeviator(router);
        var (deviation, _) = deviator.CalculateDetourDeviation(journey, currentTime: new(250), stationPosition: d);

        Assert.Equal(150, deviation);

        var coords = router.CapturedCoords!;
        Assert.Equal(d.Longitude, coords[2]);
        Assert.Equal(d.Latitude, coords[3]);
        Assert.Equal(b.Longitude, coords[4]);
        Assert.Equal(b.Latitude, coords[5]);
    }

    [Fact]
    public void CalculateDetourDeviation_DetourFasterThanRemainingTime_ReturnsZero()
    {
        var journey = new Journey(
            departure: new Time(0),
            originalDuration: new Time(1000),
            path: new Paths([new Position(0.0, 0.0), new Position(1.0, 1.0)]));

        // At t=500, remaining = 500. Detour takes 400. Deviation clamped to 0.
        var (deviation, _) = new PathDeviator(new CapturingStubRouter(detourDuration: 400))
            .CalculateDetourDeviation(journey, currentTime: new(500), stationPosition: new(0.5, 0.5));

        Assert.Equal(0, deviation);
    }

    [Fact]
    public void CalculateDetourDeviation_OsrmReturnsNegativeDuration_Throws()
    {
        var journey = new Journey(
            departure: new Time(0),
            originalDuration: new Time(1000),
            path: new Paths([new Position(0.0, 0.0), new Position(1.0, 1.0)]));

        Assert.Throws<InvalidOperationException>(() =>
            new PathDeviator(new CapturingStubRouter(detourDuration: -1))
                .CalculateDetourDeviation(journey, currentTime: new(500), stationPosition: new(0.5, 0.5)));
    }
}