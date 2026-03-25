namespace Testing;

using Core.Shared;
using Core.Vehicles;
using Core.Charging;
using Engine.Routing;
using Core.Routing;

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
        var preferences = new Preferences(0.5f, 0.9f, 50.0f);
        var journey = new Journey(0, 0, new Paths(
        [
            new(1, 1),
            new(1, 2),
        ]));
        ushort efficiency = 150;
        var ev = new EV(battery, preferences, journey, efficiency);
        var energyPrices = new EnergyPrices(new FileInfo("data/energy_prices.csv"));
        var stations = new Dictionary<ushort, Station>
        {
            { 1, new Station(1, "Station A", "Address A", new Position(0.5, 0.5), chargers: [], new Random(), energyPrices) },
            { 2, new Station(2, "Station B", "Address B", new Position(2.0, 2.0), chargers: [], new Random(), energyPrices) },
            { 3, new Station(3, "Station C", "Address C", new Position(0.1, 0.1), chargers: [], new Random(), energyPrices) },
        };

        var nearbyStations = new List<ushort> { 1, 2, 3 };
        var reachableStations = ReachableStations.FindReachableStations(path, ev, stations, nearbyStations, preferences.MaxPathDeviation);

        Assert.Contains((ushort)1, reachableStations);
        Assert.DoesNotContain((ushort)2, reachableStations);
        Assert.Contains((ushort)3, reachableStations);
    }
}
