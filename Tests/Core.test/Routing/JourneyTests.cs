namespace Core.test.Routing;

using Core.Routing;
using Core.Shared;

public class JourneyTests
{
    [Fact]
    public void JourneyInterpolationOnePointAtDurationEnd()
    {
        var singlePoint = new Position(1,1);
        var waypoints = new List<Position>
        {
            singlePoint,
        };
        var journey = new Journey(departure: 0, duration: 2, distanceMeters: 10, new Paths(waypoints));

        var expectedEndpoint = journey.CurrentPosition(2);
        Assert.Equal(singlePoint, expectedEndpoint);
    }

    [Fact]
    public void JourneyInterpolationOnePointAtDurationStart()
    {
        var singlePoint = new Position(1,1);
        var waypoints = new List<Position>
        {
            singlePoint,
        };
        var journey = new Journey(departure: 0, duration: 2, distanceMeters: 10, new Paths(waypoints));

        var expectedEndpoint = journey.CurrentPosition(0);
        Assert.Equal(singlePoint, expectedEndpoint);
    }

    [Fact]
    public void JourneyInterpolationOn2SamePointsStart()
    {
        var singlePoint = new Position(1,1);
        var waypoints = new List<Position>
        {
            singlePoint,
            singlePoint,
        };
        var journey = new Journey(departure: 0, duration: 2, distanceMeters: 10, new Paths(waypoints));

        var expectedEndpoint = journey.CurrentPosition(0);
        Assert.Equal(singlePoint, expectedEndpoint);
    }

    [Fact]
    public void JourneyInterpolationOn2SamePointsEnd()
    {
        var singlePoint = new Position(1,1);
        var waypoints = new List<Position>
        {
            singlePoint,
            singlePoint,
        };
        var journey = new Journey(departure: 0, duration: 2, distanceMeters: 10, new Paths(waypoints));

        var expectedEndpoint = journey.CurrentPosition(2);
        Assert.Equal(singlePoint, expectedEndpoint);
    }

    [Fact]
    public void JourneyInterpolationEnd()
    {
        var waypoints = new List<Position>
        {
            new(1, 1),
            new(1, 2),
        };
        var journey = new Journey(departure: 0, duration: 2, distanceMeters: 10, new Paths(waypoints));
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
        var journey = new Journey(departure: 0, duration: 2, distanceMeters: 10, new Paths(waypoints));
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
        var journey = new Journey(departure: 0, duration: 2, distanceMeters: 10, new Paths(waypoints));
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
        var journey = new Journey(departure: 0, duration: 4, distanceMeters: 10, new Paths(waypoints));
        var expectedEndpoint = journey.CurrentPosition(3);
        Assert.Equal(waypoints[3].Latitude, expectedEndpoint.Latitude);
    }

    [Fact]
    public void TimeToPointTest()
    {
        var waypoints = new List<Position>
        {
            new(1, 1),
            new(1, 2),
        };
        var journey = new Journey(departure: 0, duration: 2, distanceMeters: 0, new Paths(waypoints));
        var expectedTime = journey.DurationToWayPoint(new Position(1, 1.5));
        Assert.Equal(1U, expectedTime);
    }

    [Fact]
    public void GetPathFromCurrentPositionTest()
    {
        var waypoints = new List<Position>
        {
            new(1, 1),
            new(1, 2),
            new(1, 3),
        };
        var journey = new Journey(departure: 0, duration: 2, distanceMeters: 0, new Paths(waypoints));
        var expectedPath = journey.GetPathFromCurrentPosition(1);
        Assert.Equal(new Position(1, 2), expectedPath.Waypoints[0]);
        Assert.Equal(new Position(1, 3), expectedPath.Waypoints[1]);
    }
}
