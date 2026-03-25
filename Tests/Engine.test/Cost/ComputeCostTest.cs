namespace Engine.test.Cost;

using Core.Charging;
using Core.Routing;
using Core.Shared;
using Core.Vehicles;
using Engine.Cost;
using Engine.test.Builders;

public class ComputeCostTest
{
    [Fact]
    public void Compute_OnlyPathDeviationWeighted_SelectsLowerDeviationStation()
    {
        var costStore = new TestData.StubCostStore(new CostWeights(PathDeviation: 1));
        var computeCost = new ComputeCost(costStore);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 100),
            TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            new Journey(new Time(0), new Time(500), new Paths([new Position(0, 0), new Position(1, 1)])),
            150);

        var stationA = TestData.Station(id: 1, pos: new Position(0, 0));
        var stationB = TestData.Station(id: 2, pos: new Position(0, 0));

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
        var costStore = new TestData.StubCostStore(new CostWeights(EffectiveQueueSize: 1));
        var computeCost = new ComputeCost(costStore);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 100),
            TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            new Journey(new Time(0), new Time(500), new Paths([new Position(0, 0), new Position(1, 1)])),
            150);

        var lowQueueStation = TestData.Station(id: 1, queueSize: 1);
        var highQueueStation = TestData.Station(id: 2, queueSize: 3);

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
        var costStore = new TestData.StubCostStore(new CostWeights(PathDeviation: 1, EffectiveQueueSize: 1));
        var computeCost = new ComputeCost(costStore);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 100),
            TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            new Journey(new Time(0), new Time(500), new Paths([new Position(0, 0), new Position(1, 1)])),
            150);

        var stationA = TestData.Station(id: 1, queueSize: 5);
        var stationB = TestData.Station(id: 2, queueSize: 1);

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
        var costStore = new TestData.StubCostStore(new CostWeights(PathDeviation: 1, EffectiveQueueSize: 1));
        var computeCost = new ComputeCost(costStore);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 100),
            TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            new Journey(new Time(0), new Time(500), new Paths([new Position(0, 0), new Position(1, 1)])),
            150);

        var stationA = TestData.Station(id: 1, queueSize: 0);
        var stationB = TestData.Station(id: 2, queueSize: 0);

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
        var costStore = new TestData.StubCostStore(new CostWeights(PriceSensitivity: 1));
        var computeCost = new ComputeCost(costStore);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 50),
            TestData.Preferences(PriceSensitivity: 1.0f, MinAcceptableCharge: 20f),
            new Journey(new Time(0), new Time(500), new Paths([new Position(0, 0), new Position(1, 1)])),
            150);

        // Use deterministic energy prices to control station costs
        var cheapEnergyPrices = new TestData.FixedEnergyPrices(2.0f);
        var expensiveEnergyPrices = new TestData.FixedEnergyPrices(4.0f);

        var cheapStation = TestData.Station(id: 1, pos: new Position(0, 0), energyPrices: cheapEnergyPrices);
        var expensiveStation = TestData.Station(id: 2, pos: new Position(1, 1), energyPrices: expensiveEnergyPrices);

        // Trigger price calculation (caches price internally)
        cheapStation.UpdatePrice(DayOfWeek.Monday, 12);
        expensiveStation.UpdatePrice(DayOfWeek.Monday, 12);

        var stations = new[] { cheapStation, expensiveStation };
        var journeys = (
            duration: new[] { 500f, 500f },
            distance: new[] { 10f, 10f },
            polyline: new[] { "polyline-a", "polyline-b" });

        var bestStation = computeCost.Compute(ref ev, stations, journeys);

        // Cheaper station (2.0 DKK/kWh) should be selected over expensive (4.0 DKK/kWh)
        Assert.Same(cheapStation, bestStation);
    }

    [Fact]
    public void Compute_NoStations_ThrowsNoNullAllowedException()
    {
        var costStore = new TestData.StubCostStore(new CostWeights(PathDeviation: 1));
        var computeCost = new ComputeCost(costStore);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 100),
            TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            new Journey(new Time(0), new Time(500), new Paths([new Position(0, 0), new Position(1, 1)])),
            150);

        var stations = Array.Empty<Station>();
        var journeys = (
            duration: Array.Empty<float>(),
            distance: Array.Empty<float>(),
            polyline: Array.Empty<string>());

        Assert.Throws<System.Data.NoNullAllowedException>(() =>
            computeCost.Compute(ref ev, stations, journeys));
    }
}