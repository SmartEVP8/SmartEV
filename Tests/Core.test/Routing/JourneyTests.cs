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
        var journey = new Journey(departure: 0, duration: 2, distanceMeters: 10, waypoints);
        var expectedEndpoint = journey.GetCurrentPosition(2);
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
        var journey = new Journey(departure: 0, duration: 2, distanceMeters: 10, waypoints);
        var expectedEndpoint = journey.GetCurrentPosition(0);
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
        var journey = new Journey(departure: 0, duration: 2, distanceMeters: 10, waypoints);
        var expectedEndpoint = journey.GetCurrentPosition(1);
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
        var journey = new Journey(departure: 0, duration: 4, distanceMeters: 10, waypoints);
        var expectedEndpoint = journey.GetCurrentPosition(3);
        Assert.Equal(waypoints[3].Latitude, expectedEndpoint.Latitude);
    }

    [Fact]
    public void GetCurrentPosition_DoesNotMutateJourney()
    {
        var waypoints = new List<Position>
        {
            new(1, 1),
            new(1, 2),
        };

        var journey = new Journey(departure: 0, duration: 2, distanceMeters: 10, waypoints);

        _ = journey.GetCurrentPosition(1);

        Assert.Equal(0u, journey.Current.Departure.Seconds);
        Assert.InRange(journey.Current.DistanceKm, 0.0099f, 0.0101f);
    }

    [Fact]
    public void EtaToNextStop_IsComputedCorrectly()
    {
        var waypoints = Enumerable.Range(1, 10)
            .Select(i => new Position((double)i, (double)i))
            .ToList();

        var journey = new Journey(departure: 100, duration: 60, distanceMeters: 100, waypoints);
        var nextStop = waypoints[5];

        journey.UpdateRoute(waypoints, nextStop, departure: 100, duration: 60, newDistanceKm: 0.1f);

        Assert.True(journey.Current.EtaToNextStop > 100);
        Assert.True(journey.Current.EtaToNextStop < 100 + 60);
    }

    [Fact]
    public void ThrowsWhenCurrentTimeAfterEtaToNextStop()
    {
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(1, 1),
            new(2, 2),
            new(3, 3),
        };

        var journey = new Journey(departure: 0, duration: 100, distanceMeters: 1000, waypoints);
        var nextStop = waypoints[2];
        journey.UpdateRoute(waypoints, nextStop, departure: 0, duration: 100, newDistanceKm: 1.0f);

        var etaToNextStop = journey.Current.EtaToNextStop;
        const uint outsideApproxTolerance = 31;

        var ex = Assert.Throws<ArgumentException>(() => journey.GetCurrentPosition(etaToNextStop + outsideApproxTolerance));
        Assert.Contains("after ETA to next stop", ex.Message);
    }

    [Fact]
    public void DeriveNewWaypoints_StopsAtNextStop()
    {
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(1, 1),
            new(2, 2),
            new(3, 3),
            new(4, 4),
        };

        var journey = new Journey(departure: 0, duration: 100, distanceMeters: 1000, waypoints);
        var nextStop = waypoints[2];
        journey.UpdateRoute(waypoints, nextStop, departure: 0, duration: 100, newDistanceKm: 1.0f);

        var derivedPos = journey.GetCurrentPosition(30);
        Assert.NotNull(derivedPos);

        Assert.True(journey.Current.Waypoints.Count > 0);
    }

    [Fact]
    public void FindNextStopIndex_WithTolerance_MatchesClosestWaypoint()
    {
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(1, 1),
            new(2, 2),
            new(3, 3),
        };

        var journey = new Journey(departure: 0, duration: 100, distanceMeters: 1000, waypoints);

        var nextStopWithinTolerance = new Position(2.000005, 2.000005);
        journey.UpdateRoute(waypoints, nextStopWithinTolerance, departure: 0, duration: 100, newDistanceKm: 1.0f);

        Assert.True(journey.Current.EtaToNextStop > 0);
        Assert.True(journey.Current.EtaToNextStop <= journey.Current.Eta);
    }

    [Fact]
    public void DurationToNextStop_AtEnd_EqualsTotalDuration()
    {
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(1, 1),
            new(2, 2),
        };

        var journey = new Journey(departure: 0, duration: 60, distanceMeters: 100, waypoints);
        var nextStop = waypoints[^1];
        journey.UpdateRoute(waypoints, nextStop, departure: 0, duration: 60, newDistanceKm: 0.1f);

        Assert.Equal(journey.Current.EtaToNextStop, journey.Current.Eta);
    }

    [Fact]
    public void DurationToNextStop_NotOnRoute_EqualsTotalDuration()
    {
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(1, 1),
            new(2, 2),
        };

        var nextStopNotOnRoute = new Position(99, 99);
        var journey = new Journey(departure: 0, duration: 60, distanceMeters: 100, waypoints);
        journey.UpdateRoute(waypoints, nextStopNotOnRoute, departure: 0, duration: 60, newDistanceKm: 0.1f);

        Assert.Equal(journey.Current.EtaToNextStop, journey.Current.Eta);
    }
}
