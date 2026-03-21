using Core.Routing;
using Core.Shared;
using Engine.Routing;

public class PathDeviatorTests
{
    private class StubRouter : IDestinationRouter
    {
        private readonly float _duration;
        private readonly string _polyline;

        public StubRouter(float duration, string polyline = "")
        {
            _duration = duration;
            _polyline = polyline;
        }

        public (float duration, string polyline) QueryDestination(double[] coords)
            => (_duration, _polyline);
    }

    private static Paths SimplePath() => new([new Position(0.0, 0.0), new Position(1.0, 1.0)]);

    [Fact]
    public void CalculateDetourDeviation_ReturnsExpectedDeviation()
    {
        var deviator = new PathDeviator(new StubRouter(duration: 800));
        var journey = new Journey(
            departure: new Time(0),
            originalDuration: new Time(1000),
            path: SimplePath());

        var (deviation, _) = deviator.CalculateDetourDeviation(journey, currentTime: new(500), stationPosition: new(0.5, 0.5));
        Assert.Equal(300, deviation);
    }
}