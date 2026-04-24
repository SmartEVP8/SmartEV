namespace Core.test.Routing;

using Core.Routing;
using Core.Shared;

public class JourneyTests
{
    [Fact]
    public void Journey_ZeroDuration_Throws()
    {
        var waypoints = new List<Position> { new(0, 0), new(1, 1) };

        Assert.Throws<ArgumentOutOfRangeException>(() => new Journey(0, 0, 100_000, waypoints));
    }

    [Fact]
    public void JourneyInterpolationEnd()
    {
        var waypoints = new List<Position>
        {
            new(1, 1),
            new(1, 2),
        };
        var journey = new Journey(departure: 0, duration: 2000, distanceMeters: 10, waypoints);
        var expectedEndpoint = journey.GetCurrentPosition(2000);
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
        var journey = new Journey(departure: 0, duration: 2000, distanceMeters: 10, waypoints);
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
        var journey = new Journey(departure: 0, duration: 2000, distanceMeters: 10, waypoints);
        var expectedEndpoint = journey.GetCurrentPosition(1000);
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
        var journey = new Journey(departure: 0, duration: 4000, distanceMeters: 10, waypoints);
        var expectedEndpoint = journey.GetCurrentPosition(3000);
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

        var journey = new Journey(departure: 0, duration: 2000, distanceMeters: 10, waypoints);

        _ = journey.GetCurrentPosition(1);

        Assert.Equal(0u, journey.Current.Departure.Milliseconds);
        Assert.InRange(journey.Current.DistanceKm, 0.0099f, 0.0101f);
    }

    [Fact]
    public void EtaToNextStop_IsComputedCorrectly()
    {
        var waypoints = Enumerable.Range(1, 10)
            .Select(i => new Position(i, i))
            .ToList();

        var journey = new Journey(departure: 100000, duration: 60000, distanceMeters: 100, waypoints);
        var nextStop = waypoints[5];

        journey.UpdateRoute(waypoints, nextStop, departure: 100000, duration: 60000, newDistanceKm: 0.1f);

        Assert.True(journey.Current.EtaToNextStop > 100000);
        Assert.True(journey.Current.EtaToNextStop < 100000 + 60000);
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

        var journey = new Journey(departure: 0, duration: 100000, distanceMeters: 1000, waypoints);
        var nextStop = waypoints[2];
        journey.UpdateRoute(waypoints, nextStop, departure: 0, duration: 100000, newDistanceKm: 1.0f);

        var etaToNextStop = journey.Current.EtaToNextStop;
        const uint outsideApproxTolerance = 31000;

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

        var journey = new Journey(departure: 0, duration: 100000, distanceMeters: 1000, waypoints);
        var nextStop = waypoints[2];
        journey.UpdateRoute(waypoints, nextStop, departure: 0, duration: 100000, newDistanceKm: 1.0f);

        var derivedPos = journey.GetCurrentPosition(30000);
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

        var journey = new Journey(departure: 0, duration: 100000, distanceMeters: 1000, waypoints);

        var nextStopWithinTolerance = new Position(2.000005, 2.000005);
        journey.UpdateRoute(waypoints, nextStopWithinTolerance, departure: 0, duration: 100000, newDistanceKm: 1.0f);

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

        var journey = new Journey(departure: 0, duration: 60000, distanceMeters: 100, waypoints);
        var nextStop = waypoints[^1];
        journey.UpdateRoute(waypoints, nextStop, departure: 0, duration: 60000, newDistanceKm: 0.1f);

        Assert.Equal(journey.Current.EtaToNextStop, journey.Current.Eta);
    }

    [Fact]
    public void UpdateRoute_WhenNextStopNotOnRoute_ThrowsInvalidOperationException()
    {
        var waypoints = new List<Position>
    {
        new(0, 0),
        new(1, 1),
        new(2, 2),
    };

        var nextStopNotOnRoute = new Position(99, 99);
        var journey = new Journey(departure: 0, duration: 60000, distanceMeters: 100, waypoints);

        Assert.Throws<InvalidOperationException>(() =>
            journey.UpdateRoute(
                waypoints,
                nextStopNotOnRoute,
                departure: 0,
                duration: 60000,
                newDistanceKm: 0.1f));
    }

    [Fact]
    public void UpdateRouteToDestinationTest()
    {
        var waypoints = new List<Position> { new(0, 0), new(1, 1), new(2, 2) };
        var journey = new Journey(departure: 0, duration: 60000, distanceMeters: 100, waypoints);
        journey.UpdateRoute(waypoints, waypoints[1], departure: 0, duration: 60000, newDistanceKm: 0.1f);
        journey.AdvanceTo(30000);
        journey.UpdateRouteToDestination(timeAtStation: 30000);
        Assert.Equal(waypoints[^1].Latitude, journey.Current.Waypoints.Last().Latitude);
        Assert.Equal(journey.Current.Duration, journey.Current.DurationToNextStop);
        Assert.Equal(60U, journey.Current.Departure.TotalSeconds);
        Assert.Equal(30U, journey.Current.Duration.TotalSeconds);
    }

    [Fact]
    public void UpdateRouteToDestination_ZeroTime()
    {
        var waypoints = new List<Position> { new(0, 0), new(1, 1), new(2, 2) };
        var journey = new Journey(0, 60000, 1000, waypoints);
        journey.UpdateRoute(waypoints, waypoints[1], 0, 60000, 1f);

        journey.UpdateRouteToDestination(timeAtStation: 0);

        Assert.Equal(30u, journey.Current.Departure.TotalSeconds);
        Assert.Equal(waypoints[^1], journey.Current.NextStop);
    }

    [Fact]
    public void UpdateRouteToDestination_NextStopAlreadyLast_DurationEquals()
    {
        var waypoints = new List<Position> { new(0, 0), new(1, 1) };
        var journey = new Journey(0, 100000, 5000, waypoints);

        journey.UpdateRouteToDestination(timeAtStation: 10000);

        Assert.Equal(journey.Current.Duration, journey.Current.DurationToNextStop);
        Assert.Equal(110u, journey.Current.Departure.TotalSeconds);
    }

    [Fact]
    public void UpdateRouteToDestination_LargeStationTime_ShiftsDeparture()
    {
        var waypoints = new List<Position> { new(0, 0), new(1, 1) };
        var journey = new Journey(1000, 60000, 2000, waypoints);

        journey.UpdateRouteToDestination(timeAtStation: 36000);

        Assert.Equal(97u, journey.Current.Departure.TotalSeconds);
    }

    [Fact]
    public void AdvanceToTest()
    {
        var waypoints = new List<Position> { new(0, 0), new(1, 1), new(2, 2) };
        var journey = new Journey(departure: 0, duration: 60000, distanceMeters: 100, waypoints);
        var currentPos = journey.AdvanceTo(30000);
        Assert.Equal(waypoints[1].Latitude, currentPos.Latitude, precision: 3);
        Assert.Equal(30u, journey.Current.Departure.TotalSeconds);
        Assert.Equal(30u, journey.Current.Duration.TotalSeconds);
    }

    [Fact]
    public void UpdateRouteTest()
    {
        var waypoints = new List<Position> { new(0, 0), new(1, 1), new(2, 2) };
        var journey = new Journey(departure: 0, duration: 60000, distanceMeters: 100, waypoints);
        var newWaypoints = new List<Position> { new(0, 0), new(1, 1), new(2, 2), new(3, 3) };
        journey.UpdateRoute(newWaypoints, nextStop: newWaypoints[2], departure: 0, duration: 90000, newDistanceKm: 0.1f);
        Assert.NotEqual(waypoints.Last(), journey.Current.Waypoints.Last());
        Assert.Equal(newWaypoints.Last(), journey.Current.Waypoints.Last());
        Assert.Equal(60u, journey.Current.DurationToNextStop.TotalSeconds, tolerance: 10);
    }

    [Fact]
    public void TimeToDriveDistance_BasicCalculation()
    {
        var waypoints = new List<Position> { new(0, 0), new(1, 1) };
        var journey = new Journey(0, 3600000, 100000, waypoints);

        var result = journey.TimeToDriveDistance(50f);

        Assert.Equal(1800u, result.TotalSeconds);
    }

    [Fact]
    public void TimeToDriveDistance_ZeroDistance_ReturnsZero()
    {
        var waypoints = new List<Position> { new(0, 0), new(1, 1) };
        var journey = new Journey(0, 3600000, 100000, waypoints);

        var result = journey.TimeToDriveDistance(0f);

        Assert.Equal(0u, result.TotalSeconds);
    }

    [Fact]
    public void TimeToDriveDistance_CeilsUpToNextSecond()
    {
        var waypoints = new List<Position> { new(0, 0), new(1, 1) };
        var journey = new Journey(0, 3600000, 100_000, waypoints);

        var result = journey.TimeToDriveDistance(1.001f);

        Assert.Equal(36u, result.TotalSeconds);
    }

    [Fact]
    public void TimeToDriveDistance_VerySmallDistance_ReturnsAtLeastOne()
    {
        var waypoints = new List<Position> { new(0, 0), new(1, 1) };
        var journey = new Journey(0, 3600000, 100_000, waypoints);

        var result = journey.TimeToDriveDistance(0.001f);

        Assert.Equal(37u, result.Milliseconds);
    }

    [Fact]
    public void TimeToDriveDistance_LargerThanOriginal_StillCalculates()
    {
        var waypoints = new List<Position> { new(0, 0), new(1, 1) };
        var journey = new Journey(0, 3600000, 100_000, waypoints);

        var result = journey.TimeToDriveDistance(200f);

        Assert.Equal(7200u, result.TotalSeconds);
    }

    [Fact]
    public void AdvanceTo_AtDeparture_ReturnsStart()
    {
        var waypoints = new List<Position> { new(0, 0), new(10, 10) };
        var journey = new Journey(0, 3600000, 100_000, waypoints);

        var pos = journey.AdvanceTo(0);

        Assert.Equal(0, pos.Latitude);
        Assert.Equal(0, pos.Longitude);
        Assert.Equal(3600u, journey.Current.Duration.TotalSeconds);
    }

    [Fact]
    public void AdvanceTo_AtEta_ReturnsDestination()
    {
        var waypoints = new List<Position> { new(0, 0), new(10, 10) };
        var journey = new Journey(0, 3600000, 100_000, waypoints);

        var pos = journey.AdvanceTo(3600000);

        Assert.Equal(10, pos.Latitude, precision: 3);
        Assert.Equal(10, pos.Longitude, precision: 3);
        Assert.Equal(0u, journey.Current.Duration.Milliseconds);
    }

    [Fact]
    public void AdvanceTo_Midpoint_InterpolatesToHalfway()
    {
        var waypoints = new List<Position> { new(0, 0), new(10, 10) };
        var journey = new Journey(0, 100000, 10_000, waypoints);

        var pos = journey.AdvanceTo(50000);

        Assert.Equal(5, pos.Latitude, precision: 1);
        Assert.Equal(5, pos.Longitude, precision: 1);
    }

    [Fact]
    public void AdvanceTo_BeforeDeparture_Throws()
    {
        var waypoints = new List<Position> { new(0, 0), new(10, 10) };
        var journey = new Journey(100000, 200000, 10_000, waypoints);

        Assert.Throws<ArgumentException>(() => journey.AdvanceTo(50000));
    }

    [Fact]
    public void AdvanceTo_PastCompletion_Throws()
    {
        var waypoints = new List<Position> { new(0, 0), new(10, 10) };
        var journey = new Journey(0, 100000, 10_000, waypoints);

        Assert.Throws<ArgumentException>(() => journey.AdvanceTo(200000));
    }

    [Fact]
    public void AdvanceTo_PastEtaToNextStop_Throws()
    {
        var waypoints = new List<Position> { new(0, 0), new(1, 1), new(2, 2) };
        var journey = new Journey(0, 100000, 10_000, waypoints);
        journey.UpdateRoute(waypoints, waypoints[1], 0, 100000, 10f);

        var etaToNext = journey.Current.EtaToNextStop;
        Assert.Throws<ArgumentException>(() => journey.AdvanceTo(etaToNext + 40000));
    }
}
