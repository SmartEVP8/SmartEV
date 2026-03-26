namespace Engine.test.Routing;

using Core.Shared;
using Engine.Routing;
using Engine.test.Builders;
using Xunit.Abstractions;

public class ApplyNewPathToEVTests(ITestOutputHelper output)
{
    [Fact]
    public void ApplyNewPathToEV_UpdatesJourneyPath()
    {
        var ev = TestData.EV(waypoints: [
            new Position(0, 0),
            new Position(10, 10)
        ]);

        var station = TestData.Station(1, new Position(5, 5));
        var router = TestData.OSRMRouter;
        var applyNewPath = new ApplyNewPath(router);

        var originalWaypoints = ev.Journey.Path.Waypoints.ToList();

        applyNewPath.ApplyNewPathToEV(ref ev, station, new Time(0));

        Assert.NotEqual(originalWaypoints, ev.Journey.Path.Waypoints);
    }

    [Fact]
    public void ApplyNewPathToEV_PreservesTravelledWaypoints()
    {
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(5, 5),
            new(10, 10),
        };
    
        var ev = TestData.EV(
            waypoints: waypoints,
            originalDuration: new Time(100));
    
        var station = TestData.Station(1, new Position(7, 7));
        var router = TestData.OSRMRouter;
        var applyNewPath = new ApplyNewPath(router);
    
        var currentTime = new Time(50);
    
        applyNewPath.ApplyNewPathToEV(ref ev, station, currentTime);
    
        var newWaypoints = ev.Journey.Path.Waypoints;
    
        Assert.Contains(waypoints[0], newWaypoints);
    
        Assert.Contains(waypoints[1], newWaypoints);
    }

    [Fact]
    public void ApplyNewPath_DoesNotMoveEV()
    {
        var ev = TestData.EV(
            waypoints: [
                new Position(0, 0),
                new Position(5, 5),
                new Position(10, 10)
            ],
            originalDuration: new Time(100));

        var station = TestData.Station(1, new Position(7, 7));
        var applyNewPath = new ApplyNewPath(TestData.OSRMRouter);

        var currentTime = new Time(50);

        var beforePos = ev.Journey.CurrentPosition(currentTime);

        applyNewPath.ApplyNewPathToEV(ref ev, station, currentTime);

        var afterPos = ev.Journey.CurrentPosition(currentTime);

        _output.WriteLine($"Before:  {beforePos.Latitude:F10}, {beforePos.Longitude:F10}");
        _output.WriteLine($"After:   {afterPos.Latitude:F10}, {afterPos.Longitude:F10}");

        var latDiff = Math.Abs(beforePos.Latitude - afterPos.Latitude);
        var lonDiff = Math.Abs(beforePos.Longitude - afterPos.Longitude);

        _output.WriteLine($"ΔLat: {latDiff}");
        _output.WriteLine($"ΔLon: {lonDiff}");

        Assert.True(
            latDiff < 1e-6 && lonDiff < 1e-6,
            $"EV moved! Before: ({beforePos.Latitude}, {beforePos.Longitude}) " + $"After: ({afterPos.Latitude}, {afterPos.Longitude})");
    }

    private readonly ITestOutputHelper _output = output;
}