namespace Engine.test.Cost;

using Core.Charging;
using Core.Routing;
using Core.Shared;
using Core.Vehicles;
using Engine.Cost;
using Engine.test.Builders;

public class ComputeCostTest
{
    private readonly Time _time = new(0);

    [Fact]
    public void Compute_OnlyPathDeviationWeighted_SelectsLowerDeviationStation()
    {
        var costStore = new TestData.StubCostStore(new CostWeights(PathDeviation: 1));
        var stationService = new TestData.StubStationService(new Dictionary<ushort, Station>
        {
            { 1, TestData.Station(id: 1, pos: new Position(0, 0)) },
            { 2, TestData.Station(id: 2, pos: new Position(0, 0)) },
        });
        var computeCost = new ComputeCost(costStore, stationService);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 100),
            TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            new Journey(new Time(0), new Time(500), 100, new Paths([new Position(0, 0), new Position(1, 1)])),
            150);

        var stationA = stationService.GetStation(1);

        var stationDurations = new Dictionary<ushort, float>
        {
            { 1, 500f }, // Deviation = 0
            { 2, 600f }, // Deviation = 100
        };

        var bestStation = computeCost.Compute(ref ev, stationDurations, _time);

        Assert.Same(stationA, bestStation);
    }

    [Fact]
    public void Compute_OnlyQueueWeighted_SelectsShorterQueueStation()
    {
        var costStore = new TestData.StubCostStore(new CostWeights(EffectiveQueueSize: 1));
        var stationService = new TestData.StubStationService(new Dictionary<ushort, Station>
        {
            { 1, TestData.Station(id: 1, pos: new Position(0, 0), queueSize: 1) },
            { 2, TestData.Station(id: 2, pos: new Position(0, 0), queueSize: 3) },
        });
        var computeCost = new ComputeCost(costStore, stationService);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 100),
            TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            new Journey(new Time(0), new Time(500), 100, new Paths([new Position(0, 0), new Position(1, 1)])),
            150);

        var stationDurations = new Dictionary<ushort, float>
        {
            { 1, 500f },
            { 2, 500f },
        };

        var bestStation = computeCost.Compute(ref ev, stationDurations, _time);

        Assert.Same(stationService.GetStation(1), bestStation);
    }

    [Fact]
    public void Compute_MixedWeights_SelectsOptimalStation()
    {
        // Station A: low deviation (100), high queue (5)
        // Station B: high deviation (300), low queue (1)
        // With equal weights, station A wins with lower total cost
        var costStore = new TestData.StubCostStore(new CostWeights(PathDeviation: 1, EffectiveQueueSize: 1));
        var stationService = new TestData.StubStationService(new Dictionary<ushort, Station>
        {
            { 1, TestData.Station(id: 1, pos: new Position(0, 0), queueSize: 5) },
            { 2, TestData.Station(id: 2, pos: new Position(0, 0), queueSize: 1) },
        });

        var computeCost = new ComputeCost(costStore, stationService);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 100),
            TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            new Journey(new Time(0), new Time(500), 100, new Paths([new Position(0, 0), new Position(1, 1)])),
            150);

        var stationA = stationService.GetStation(1);

        var stationDurations = new Dictionary<ushort, float>
        {
            { 1, 500f }, // Deviation = 100
            { 2, 700f }, // Deviation = 300
        };

        var bestStation = computeCost.Compute(ref ev, stationDurations, _time);

        // Station A: cost = 1 * 100 (deviation) + 1 * 5^2 (queue) = 125
        // Station B: cost = 1 * 300 (deviation) + 1 * 1^2 (queue) = 301
        Assert.Same(stationA, bestStation);
    }

    [Fact]
    public void Compute_AllStationsIdenticalCost_ReturnsFirst()
    {
        var costStore = new TestData.StubCostStore(new CostWeights(PathDeviation: 1, EffectiveQueueSize: 1));
        var stationService = new TestData.StubStationService(new Dictionary<ushort, Station>
        {
            { 1, TestData.Station(id: 1, pos: new Position(0, 0), queueSize: 0) },
            { 2, TestData.Station(id: 2, pos: new Position(0, 0), queueSize: 0) },
        });
        var computeCost = new ComputeCost(costStore, stationService);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 100),
            TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            new Journey(new Time(0), new Time(500), 100, new Paths([new Position(0, 0), new Position(1, 1)])),
            150);

        var stationA = stationService.GetStation(1);

        var stationDurations = new Dictionary<ushort, float>
        {
            { 1, 500f },
            { 2, 500f },
        };

        var bestStation = computeCost.Compute(ref ev, stationDurations, _time);

        // Both have cost 0, so first in iteration order wins
        Assert.Same(stationA, bestStation);
    }

    [Fact]
    public void Compute_OnlyPriceSensitivityWeighted_SelectsCheaperStation()
    {
        var costStore = new TestData.StubCostStore(new CostWeights(PriceSensitivity: 1));
        var stationService = new TestData.StubStationService(new Dictionary<ushort, Station>
        {
            { 1, TestData.Station(id: 1, pos: new Position(0, 0), energyPrices: new TestData.FixedEnergyPrices(2.0f)) },
            { 2, TestData.Station(id: 2, pos: new Position(1, 1), energyPrices: new TestData.FixedEnergyPrices(4.0f)) },
        });
        var computeCost = new ComputeCost(costStore, stationService);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 50),
            TestData.Preferences(PriceSensitivity: 1.0f, MinAcceptableCharge: 20f),
            new Journey(new Time(0), new Time(500), 100, new Paths([new Position(0, 0), new Position(1, 1)])),
            150);

        var cheapStation = stationService.GetStation(1);

        var stationDurations = new Dictionary<ushort, float>
            {
                { 1, 500f },
                { 2, 500f },
            };

        var bestStation = computeCost.Compute(ref ev, stationDurations, _time);

        // Cheaper station (2.0 DKK/kWh) should be selected over expensive (4.0 DKK/kWh)
        Assert.Same(cheapStation, bestStation);
    }

    [Fact]
    public void Compute_NoStations_ThrowsNoNullAllowedException()
    {
        var costStore = new TestData.StubCostStore(new CostWeights(PathDeviation: 1));
        var stationService = new TestData.StubStationService(new Dictionary<ushort, Station>());
        var computeCost = new ComputeCost(costStore, stationService);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 100),
            TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            new Journey(new Time(0), new Time(500), 100, new Paths([new Position(0, 0), new Position(1, 1)])),
            150);

        var stationDurations = new Dictionary<ushort, float>();

        Assert.Throws<System.Data.NoNullAllowedException>(() =>
            computeCost.Compute(ref ev, stationDurations, _time));
    }
}
