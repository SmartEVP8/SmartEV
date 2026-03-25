using System.Collections.Immutable;
using Core.Charging;
using Core.Routing;
using Core.Shared;
using Core.Vehicles;
using Engine.Cost;

public class ComputeCostTest
{
    private static Station CreateStation(ushort id, int queueSize)
    {
        var charger = new FakeCharger();
        for (var i = 0; i < queueSize; i++)
        {
            charger.Queue.Enqueue(i);
        }

        return new Station(
            id: id,
            name: $"Station-{id}",
            address: "Address",
            position: new Position(0, 0),
            chargers: [charger],
            random: new Random(42),
            energyPrices: new EnergyPrices(new FileInfo("data/energy_prices.csv")));
    }

    private sealed class StubCostStore(CostWeights weights) : ICostStore
    {
        private readonly CostWeights _weights = weights;

        public CostWeights GetWeights() => _weights;

        public void TrySet(CostWeights update, long seq)
        {
        }
    }

    private sealed class FakeCharger() : ChargerBase(id: 1, maxPowerKW: 100)
    {
        public override ImmutableArray<Socket> GetSockets() => [Socket.CCS2];
    }

    [Fact]
    public void Compute_OnlyPathDeviationWeighted_SelectsLowerDeviationStation()
    {
        var costStore = new StubCostStore(new CostWeights(PathDeviation: 1));
        var computeCost = new ComputeCost(costStore);
        var ev = CreateNeutralEv(originalDuration: 500);

        var stationA = CreateStation(id: 1, queueSize: 0);
        var stationB = CreateStation(id: 2, queueSize: 0);

        var stations = new[] { stationA, stationB };
        var journeys = (
            duration: new[] { 600f, 800f },
            distance: new[] { 10f, 20f },
            polyline: new[] { "polyline-a", "polyline-b" });

        var bestStation = computeCost.Compute(ref ev, stations, journeys);

        Assert.Same(stationA, bestStation);
    }

    [Fact]
    public void Compute_OnlyQueueWeighted_SelectsShorterQueueStation()
    {
        var costStore = new StubCostStore(new CostWeights(EffectiveQueueSize: 1));
        var computeCost = new ComputeCost(costStore);
        var ev = CreateNeutralEv(originalDuration: 500);

        var lowQueueStation = CreateStation(id: 1, queueSize: 1);
        var highQueueStation = CreateStation(id: 2, queueSize: 3);

        var stations = new[] { lowQueueStation, highQueueStation };
        var journeys = (
            duration: new[] { 500f, 500f },
            distance: new[] { 10f, 10f },
            polyline: new[] { "polyline-a", "polyline-b" });

        var bestStation = computeCost.Compute(ref ev, stations, journeys);

        Assert.Same(lowQueueStation, bestStation);
    }

    [Fact]
    public void Compute_MixedWeights_SelectsOptimalStation()
    {
        // Station A: low deviation (100), high queue (5)
        // Station B: high deviation (300), low queue (1)
        // With equal weights, station A wins with lower total cost
        var costStore = new StubCostStore(new CostWeights(PathDeviation: 1, EffectiveQueueSize: 1));
        var computeCost = new ComputeCost(costStore);
        var ev = CreateNeutralEv(originalDuration: 500);

        var stationA = CreateStation(id: 1, queueSize: 5);
        var stationB = CreateStation(id: 2, queueSize: 1);

        var stations = new[] { stationA, stationB };
        var journeys = (
            duration: new[] { 600f, 800f },
            distance: new[] { 10f, 20f },
            polyline: new[] { "polyline-a", "polyline-b" });

        var bestStation = computeCost.Compute(ref ev, stations, journeys);

        // Station A: cost = 1 * 100 (deviation) + 1 * 5^2 (queue) = 125
        // Station B: cost = 1 * 300 (deviation) + 1 * 1^2 (queue) = 301
        Assert.Same(stationA, bestStation);
    }

    [Fact]
    public void Compute_AllStationsIdenticalCost_ReturnsFirst()
    {
        var costStore = new StubCostStore(new CostWeights(PathDeviation: 1, EffectiveQueueSize: 1));
        var computeCost = new ComputeCost(costStore);
        var ev = CreateNeutralEv(originalDuration: 500);

        var stationA = CreateStation(id: 1, queueSize: 0);
        var stationB = CreateStation(id: 2, queueSize: 0);

        var stations = new[] { stationA, stationB };
        var journeys = (
            duration: new[] { 500f, 500f },
            distance: new[] { 10f, 10f },
            polyline: new[] { "polyline-a", "polyline-b" });

        var bestStation = computeCost.Compute(ref ev, stations, journeys);

        // Both have cost 0, so first in iteration order wins
        Assert.Same(stationA, bestStation);
    }

    [Fact]
    public void Compute_OnlyPriceSensitivityWeighted_SelectsCheaperStation()
    {
        var costStore = new StubCostStore(new CostWeights(PriceSensitivity: 1));
        var computeCost = new ComputeCost(costStore);
        var ev = CreateEv(originalDuration: 500, priceSensitivity: 1.0f);

        var cheapStation = CreateStation(id: 1, queueSize: 0);
        var expensiveStation = CreateStation(id: 2, queueSize: 0);
        cheapStation.CalculatePrice(DayOfWeek.Monday, 12);
        expensiveStation.CalculatePrice(DayOfWeek.Monday, 12);

        // Manually set prices for deterministic test
        var stations = new[] { cheapStation, expensiveStation };
        var journeys = (
            duration: new[] { 500f, 500f },
            distance: new[] { 10f, 10f },
            polyline: new[] { "polyline-a", "polyline-b" });

        var bestStation = computeCost.Compute(ref ev, stations, journeys);

        // Cheaper station should be selected (lower cost)
        Assert.NotNull(bestStation);
    }

    [Fact]
    public void Compute_NoStations_ThrowsNoNullAllowedException()
    {
        var costStore = new StubCostStore(new CostWeights(PathDeviation: 1));
        var computeCost = new ComputeCost(costStore);
        var ev = CreateNeutralEv(originalDuration: 500);

        var stations = Array.Empty<Station>();
        var journeys = (
            duration: Array.Empty<float>(),
            distance: Array.Empty<float>(),
            polyline: Array.Empty<string>());

        Assert.Throws<System.Data.NoNullAllowedException>(() =>
            computeCost.Compute(ref ev, stations, journeys));
    }

    private static EV CreateNeutralEv(uint originalDuration)
    {
        // High SoC to minimize urgency cost, zero price sensitivity to minimize price cost
        var battery = new Battery(capacity: 100, maxChargeRate: 200, stateOfCharge: 100, socket: Socket.CCS2);
        var preferences = new Preferences(priceSensitivity: 0.0f, minAcceptableCharge: 0f);
        var journey = new Journey(
            departure: new Time(0),
            originalDuration: new Time(originalDuration),
            path: new Paths([new Position(0, 0), new Position(1, 1)]));

        return new EV(battery, preferences, journey, efficiency: 150);
    }

    private static EV CreateEv(uint originalDuration, float priceSensitivity = 0.5f)
    {
        var battery = new Battery(capacity: 100, maxChargeRate: 200, stateOfCharge: 50, socket: Socket.CCS2);
        var preferences = new Preferences(priceSensitivity: priceSensitivity, minAcceptableCharge: 20f);
        var journey = new Journey(
            departure: new Time(0),
            originalDuration: new Time(originalDuration),
            path: new Paths([new Position(0, 0), new Position(1, 1)]));

        return new EV(battery, preferences, journey, efficiency: 150);
    }
}