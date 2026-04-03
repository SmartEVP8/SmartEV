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
        var energyPrices = new TestData.FixedEnergyPrices(2.0f);
        var stationService = new TestData.StubStationService(new Dictionary<ushort, Station>
        {
            { 1, TestData.Station(id: 1, pos: new (0, 0)) },
            { 2, TestData.Station(id: 2, pos: new (0, 0)) },
        });
        var computeCost = new ComputeCost(costStore, stationService, energyPrices);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 100),
            TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            new Journey(new Time(0), new Time(500), 100, new List<Position>([new(0, 0), new(1, 1)])),
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
        var energyPrices = new TestData.FixedEnergyPrices(2.0f);
        var stationService = new TestData.StubStationService(new Dictionary<ushort, Station>
        {
            { 1, TestData.Station(id: 1, pos: new (0, 0), queueSize: 1) },
            { 2, TestData.Station(id: 2, pos: new (0, 0), queueSize: 3) },
        });
        var computeCost = new ComputeCost(costStore, stationService, energyPrices);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 100),
            TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            new Journey(new Time(0), new Time(500), 100, new List<Position>([new(0, 0), new(1, 1)])),
            150);

        var stationDurations = new Dictionary<ushort, float>
        {
            { 1, 500f },
            { 2, 500f },
        };

        var bestStation = computeCost.Compute(ref ev, stationDurations, _time);

        Assert.Same(stationService.GetStation(1), bestStation);
    }

    [Theory]
    [InlineData(0.5f, 2)] // Low deviation weight -> Station 2 wins (much shorter queue)
    [InlineData(7.0f, 1)] // High deviation weight -> Station 1 wins (no deviation)
    public void Compute_QueueVsPathDeviation_SelectsBasedOnWeights(float pathDeviationWeight, ushort expectedStationId)
    {
        var costStore = new TestData.StubCostStore(
            new CostWeights(
                PathDeviation: pathDeviationWeight,
                EffectiveQueueSize: 1.0f));

        var energyPrices = new TestData.FixedEnergyPrices(2.0f);

        var stationService = new TestData.StubStationService(new Dictionary<ushort, Station>
        {
            { 1, TestData.Station(id: 1, pos: new (0, 0), queueSize: 4) },
            { 2, TestData.Station(id: 2, pos: new (0, 0), queueSize: 1) },
        });

        var computeCost = new ComputeCost(costStore, stationService, energyPrices);

        var ev = new EV(
            TestData.Battery(stateOfCharge: 0.5f),
            TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            new Journey(new Time(0), new Time(1000), 1000, new List<Position>([new(0, 0), new(1, 1)])),
            150);

        var stationDurations = new Dictionary<ushort, float>
        {
            { 1, 1000f }, // Deviation = 0 min
            { 2, 1600f }, // Deviation = (1600 - 1000) / 60 = 10 min
        };

        // Tipping point: 3 cars (queue diff) vs 10 mins (deviation).
        // Weight < 0.3/min -> Queue wins. Weight > 0.3/min -> Deviation wins.
        var bestStation = computeCost.Compute(ref ev, stationDurations, _time);
        Assert.Equal(expectedStationId, bestStation.Id);
    }

    [Theory]
    [InlineData(30, 2)] // Low battery -> Station 2 (Shorter queue beats deviation)
    [InlineData(90, 2)] // High battery -> Station 2 (Shorter queue)
    public void Compute_UrgencyVsQueue_SelectsBasedOnBattery(int stateOfCharge, ushort expectedStationId)
    {
        var weights = new CostWeights(PathDeviation: 0.5f, EffectiveQueueSize: 1.0f, Urgency: 10.0f);
        var costStore = new TestData.StubCostStore(weights);
        var energyPrices = new TestData.FixedEnergyPrices(2.0f);

        var stationService = new TestData.StubStationService(new Dictionary<ushort, Station>
        {
            { 1, TestData.Station(id: 1, pos: new (0, 0), queueSize: 5) },
            { 2, TestData.Station(id: 2, pos: new (0, 0), queueSize: 1) },
        });

        var computeCost = new ComputeCost(costStore, stationService, energyPrices);

        var ev = new EV(
            TestData.Battery(stateOfCharge: stateOfCharge),
            TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 20f),
            new Journey(new Time(0), new Time(1000), 1000, new List<Position>([new(0, 0), new(1, 1)])),
            150);

        var stationDurations = new Dictionary<ushort, float>
        {
            { 1, 1000f }, // 0 min deviation
            { 2, 1500f }, // ~8 min deviation
        };

        var bestStation = computeCost.Compute(ref ev, stationDurations, _time);
        Assert.Equal(expectedStationId, bestStation.Id);
    }

    [Fact]
    public void Compute_AllStationsIdenticalCost_ReturnsFirst()
    {
        var costStore = new TestData.StubCostStore(new CostWeights(PathDeviation: 1, EffectiveQueueSize: 1));
        var energyPrices = new TestData.FixedEnergyPrices(2.0f);
        var stationService = new TestData.StubStationService(new Dictionary<ushort, Station>
        {
            { 1, TestData.Station(id: 1, pos: new (0, 0), queueSize: 0) },
            { 2, TestData.Station(id: 2, pos: new (0, 0), queueSize: 0) },
        });
        var computeCost = new ComputeCost(costStore, stationService, energyPrices);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 100),
            TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            new Journey(new Time(0), new Time(500), 100, new List<Position>([new(0, 0), new(1, 1)])),
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
        var energyPrices = new TestData.FixedEnergyPrices(2.0f);
        var stationService = new TestData.StubStationService(new Dictionary<ushort, Station>
        {
            { 1, TestData.Station(id: 1, pos: new (0, 0), energyPrices: new TestData.FixedEnergyPrices(2.0f)) },
            { 2, TestData.Station(id: 2, pos: new (1, 1), energyPrices: new TestData.FixedEnergyPrices(4.0f)) },
        });
        var computeCost = new ComputeCost(costStore, stationService, energyPrices);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 50),
            TestData.Preferences(PriceSensitivity: 1.0f, MinAcceptableCharge: 20f),
            new Journey(new Time(0), new Time(500), 100, new List<Position>([new(0, 0), new(1, 1)])),
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
        var energyPrices = new TestData.FixedEnergyPrices(2.0f);
        var computeCost = new ComputeCost(costStore, stationService, energyPrices);
        var ev = new EV(
            TestData.Battery(stateOfCharge: 100),
            TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            new Journey(new Time(0), new Time(500), 100, new List<Position>([new(0, 0), new(1, 1)])),
            150);

        var stationDurations = new Dictionary<ushort, float>();

        Assert.Throws<ArgumentNullException>(() =>
            computeCost.Compute(ref ev, stationDurations, _time));
    }

    /// <summary>
    /// Test verifies that path deviation cost is calculated correctly when an EV has already been rerouted through a station.
    /// </summary>
    [Fact]
    public void Compute_AfterBeingRerouted_StillCalculatesPathDeviationCorrectly()
    {
        var costStore = new TestData.StubCostStore(new CostWeights(PathDeviation: 1));
        var energyPrices = new TestData.FixedEnergyPrices(2.0f);
        var stationService = new TestData.StubStationService(new Dictionary<ushort, Station>
        {
            { 1, TestData.Station(id: 1, pos: new (0, 0)) },
            { 2, TestData.Station(id: 2, pos: new (0, 0)) },
        });
        var computeCost = new ComputeCost(costStore, stationService, energyPrices);

        var ev = TestData.EV(
            waypoints: [new(0, 0), new(1, 1)],
            battery: TestData.Battery(stateOfCharge: 50),
            preferences: TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            originalDuration: 1000);

        ev.Journey.UpdateRoute(
            waypoints: new List<Position>([new(0, 0), new(0.5f, 0.5f), new(1, 1)]),
            nextStop: new(1, 1),
            departure: new Time(200),
            duration: new Time(900),
            newDistanceKm: 150);

        var stationDurations = new Dictionary<ushort, float> { { 1, 600f }, { 2, 650f } };
        var bestStation = computeCost.Compute(ref ev, stationDurations, new Time(250));

        Assert.Equal(1, bestStation.Id);
    }

    /// <summary>
    /// Test simulates multiple reroutes: Original route A->B, then detour to Station A, then detour again to Station B.
    /// Validates that path deviation cost is calculated based on the last updated route and departure time.
    /// </summary>
    [Fact]
    public void Compute_MultipleReroutesAToStationAToB_SelectsCorrectStationAtLastDetour()
    {
        // Test simulates full chain: A -> Station A -> B, then detour again to a station
        // Validates that path deviation cost works through multiple reroutes
        var costStore = new TestData.StubCostStore(new CostWeights(PathDeviation: 1));
        var energyPrices = new TestData.FixedEnergyPrices(2.0f);
        var stationService = new TestData.StubStationService(new Dictionary<ushort, Station>
        {
            { 10, TestData.Station(id: 10, pos: new (0.5f, 0.5f)) },
            { 20, TestData.Station(id: 20, pos: new (0, 0)) },
            { 30, TestData.Station(id: 30, pos: new (0, 0)) },
        });
        var computeCost = new ComputeCost(costStore, stationService, energyPrices);

        var ev = TestData.EV(
            waypoints: [new(0, 0), new(1, 1)],
            battery: TestData.Battery(stateOfCharge: 50),
            preferences: TestData.Preferences(PriceSensitivity: 0.0f, MinAcceptableCharge: 0f),
            originalDuration: 1000);

        // First reroute at time 100: A -> Station A (10) -> B
        ev.Journey.UpdateRoute(
            waypoints: new List<Position>([new(0, 0), new(0.5f, 0.5f), new(1, 1)]),
            nextStop: new(1, 1),
            departure: new Time(100),
            duration: new Time(950),
            newDistanceKm: 150);

        // Second reroute at time 400: A -> Station A -> Station B (20) -> final destination
        // Route now ends at 400 + 800 = 1200, so at time 600 there are 600 seconds remaining
        ev.Journey.UpdateRoute(
            waypoints: new List<Position>([new(0, 0), new(0.5f, 0.5f), new(0.8f, 0.8f), new(1, 1)]),
            nextStop: new(1, 1),
            departure: new Time(400),
            duration: new Time(800),
            newDistanceKm: 150);

        // At time 600, choose between two more charging stations
        // Station 20: 600 second detour = 0 deviation (perfect match)
        // Station 30: 650 second detour = ~0.83 min deviation
        var stationDurations = new Dictionary<ushort, float> { { 20, 600f }, { 30, 650f } };
        var bestStation = computeCost.Compute(ref ev, stationDurations, new Time(600));

        Assert.Equal(20, bestStation.Id);
    }
}
