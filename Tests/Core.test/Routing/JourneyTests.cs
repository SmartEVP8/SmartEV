namespace Core.test.Routing;

using Core.Routing;
using Core.Shared;

public class JourneyTests
{
    [Fact]
    public void JourneyInterpolationEnd()
    {
        var waypoints = new List<Position>
        {
            new(1, 1),
            new(1, 2),
        };
        var journey = new Journey(departure: 0, originalDuration: 2, new Paths(waypoints));
        var expectedEndpoint = journey.CurrentPosition(2);
        Assert.Equal(waypoints[1].Latitude, expectedEndpoint.Latitude);
    }

    [Fact]
    public void JourneyInterpolationStart()
    {
        var waypoints = new List<Position>
        {
            new(1, 1),
            new(1, 2),
        };
        var journey = new Journey(departure: 0, originalDuration: 2, new Paths(waypoints));
        var expectedEndpoint = journey.CurrentPosition(0);
        Assert.Equal(waypoints[0].Latitude, expectedEndpoint.Latitude);
    }

    [Fact]
    public void JourneyInterpolationBetween()
    {
        var waypoints = new List<Position>
        {
            new(1, 1),
            new(1, 2),
        };
        var journey = new Journey(departure: 0, originalDuration: 2, new Paths(waypoints));
        var expectedEndpoint = journey.CurrentPosition(1);
        Assert.Equal(1.5, expectedEndpoint.Latitude);
    }

    [Fact]
    public void JourneyInterpolationBetweenMultipleLineSegments()
    {
        var waypoints = new List<Position>
        {
            new(1, 1),
            new(1, 2),
            new(1, 3),
            new(1, 4),
            new(1, 5),
        };
        var journey = new Journey(departure: 0, originalDuration: 4, new Paths(waypoints));
        var expectedEndpoint = journey.CurrentPosition(3);
        Assert.Equal(waypoints[3].Latitude, expectedEndpoint.Latitude);
    }
}
