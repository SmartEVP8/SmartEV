namespace Testing;

using Core.Shared;
using Core.Vehicles.Configs;
using Core.Vehicles;
using Core.Charging;
using Engine.Routing;
public class ReachableStationsTests
{
    [Fact]
    public void FindReachableStationse()
    {
        var path = new Paths(
        [
            new Position(0, 0),
            new Position(1, 1),
        ]);
        var battery = new Battery(100, 50, 50, Socket.CCS2);
        var preferences = new Preferences(0.5f, 0.9f);
        var evConfig = new EVConfig("TestModel", 1f, "TestCategory", new BatteryConfig(50, 100, Socket.CCS2), 150);
        var ev = new EV(1, battery, preferences, evConfig);
        var stations = new Dictionary<ushort, Station>
        {
            { 1, new Station(1, "Station A", "Address A", new Position(0.5, 0.5), null, 0.5f, new Random()) },
            { 2, new Station(2, "Station B", "Address B", new Position(2.0, 2.0), null, 0.5f, new Random()) },
            { 3, new Station(3, "Station C", "Address C", new Position(0.1, 0.1), null, 0.5f, new Random()) },
        };

        var nearbyStations = new List<ushort> { 1, 2, 3 };
        var reachableStations = ReachableStations.FindReachableStations(path, ev, stations, nearbyStations, 50);

        Assert.Contains((ushort)1, reachableStations);
        Assert.DoesNotContain((ushort)2, reachableStations);
        Assert.Contains((ushort)3, reachableStations);
    }
}
