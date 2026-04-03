namespace Testing;

using Core.Shared;
using Core.Vehicles;
using Core.Charging;
using Engine.Routing;
using Engine.test.Builders;

public class ReachableStationsTests
{
    [Fact]
    public void FindReachableStationse()
    {
        var waypoints = new List<Position>(
        [
            new (0, 0),
            new (1, 1),
        ]);

        var preferences = new Preferences(0.5f, 0.9f, 50.0f);
        var ev = TestData.EV(
                waypoints,
                TestData.Battery(capacity: 100, maxChargeRate: 150, stateOfCharge: 50));

        var stations = new Dictionary<ushort, Station>
        {
            { 1, TestData.Station(1, new (0.5, 0.5)) },
            { 2, TestData.Station(2, new (2.0, 2.0)) },
            { 3, TestData.Station(3, new (0.1, 0.1)) },
        };

        var nearbyStations = new List<ushort> { 1, 2, 3 };
        var reachableStations = ReachableStations.FindReachableStations(waypoints, ev, stations, nearbyStations, preferences.MaxPathDeviation);

        Assert.Contains((ushort)1, reachableStations);
        Assert.DoesNotContain((ushort)2, reachableStations);
        Assert.Contains((ushort)3, reachableStations);
    }
}
