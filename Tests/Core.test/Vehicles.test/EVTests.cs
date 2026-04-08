namespace Core.test.Vehicles.test;

using Core.Routing;
using Core.Shared;
using Core.Vehicles;
using Engine.test.Builders;

public class EVTests
{
    [Fact]
    public void Advance_UpdatesJourneyAndConsumesEnergy()
    {
        var battery = new Battery(100, 100, 1f, Socket.CCS2);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(10, 0),
        };
        var journey = new Journey(departure: 0, duration: 100, distanceMeters: 10_000, waypoints);
        var ev = new EV(battery, preferences, journey, 100);

        var currentPosition = ev.Advance(50);

        Assert.Equal(new Position(5, 0), currentPosition);
        Assert.Equal(50u, ev.Journey.Current.Departure.Seconds);
        Assert.InRange(ev.Journey.Current.DistanceKm, 4.999f, 5.001f);
        Assert.InRange(ev.Battery.StateOfCharge, 0.9949f, 0.9951f);
    }

    [Fact]
    public void CalcDesiredSoC_ReturnsCorrectValue()
    {
        var battery = new Battery(100, 100, 0.5f, Socket.CCS2);
        var preferences = new Preferences(1f, 0.3f, 10.0f);
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(5, 0),
            new(10, 0),
        };
        var journey = new Journey(departure: 0, duration: 25200, distanceMeters: 400000, waypoints);
        journey.UpdateRoute(waypoints, waypoints[1], departure: 0, duration: 25200, newDistanceKm: 400);
        var ev = new EV(battery, preferences, journey, 100);

        var desiredSoC = ev.CalcDesiredSoC(12600);

        Assert.Equal(0.51, desiredSoC, precision: 2);
    }

    [Fact]
    public void CalcDesiredSoC_ZeroBatteryCapacity_Throws()
    {
        var battery = new Battery(0, 100, 0.5f, Socket.CCS2);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(1, 0),
        };
        var journey = new Journey(departure: 0, duration: 100, distanceMeters: 1_000, waypoints);
        var ev = new EV(battery, preferences, journey, 150);

        var ex = Assert.Throws<InvalidOperationException>(() => ev.CalcDesiredSoC(arrivalAtStation: 10));
        Assert.Contains("Battery capacity", ex.Message);
    }

    [Fact]
    public void CalcDesiredSoC_CannotReachWithSingleCharge_CapsAt80Percent()
    {
        // Huge remaining distance → energyToDest exceeds battery capacity
        var waypoints = new List<Position> { new(0, 0), new(10, 0) };
        var journey = new Journey(departure: 0, duration: 25200, distanceMeters: 2_000_000, waypoints);
        journey.UpdateRoute(waypoints, waypoints[^1], 0, 25200, 2000f);
        var battery = new Battery(50, 100, 0.5f, Socket.CCS2);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var ev = new EV(battery, preferences, journey, 200);

        var desiredSoC = ev.CalcDesiredSoC(0);

        Assert.Equal(0.81f, desiredSoC, precision: 2);
    }

    [Fact]
    public void CalcDesiredSoC_AtDestination_ReturnsMinAcceptablePlusBuffer()
    {
        var waypoints = new List<Position> { new(0, 0), new(1, 0) };
        var journey = new Journey(departure: 0, duration: 100, distanceMeters: 1000, waypoints);
        var battery = new Battery(100, 100, 0.5f, Socket.CCS2);
        var preferences = new Preferences(1f, 0.15f, 10.0f);
        var ev = new EV(battery, preferences, journey, 150);

        var desiredSoC = ev.CalcDesiredSoC(100);

        Assert.Equal(0.16f, desiredSoC, precision: 2);
    }

    [Fact]
    public void CanCompleteJourney_ReturnsTrueWhenEnoughCharge()
    {
        var ev = TestData.EV(battery: TestData.Battery(stateOfCharge: 1f));
        Assert.True(ev.CanCompleteJourney(reserve: 0.1f));
    }

    [Fact]
    public void CanCompleteJourney_ReturnsFalseWhenNotEnoughCharge()
    {
        var ev = TestData.EV(battery: TestData.Battery(stateOfCharge: 0.001f));
        Assert.False(ev.CanCompleteJourney(reserve: 0.1f));
    }

    [Fact]
    public void CanCompleteJourney_ExactlyEnoughCharge_ReturnsTrue()
    {
        var ev = TestData.EV(
            battery: TestData.Battery(capacity: 100, stateOfCharge: 0.1002f),
            efficiency: 150);

        Assert.True(ev.CanCompleteJourney(reserve: 0.1f));
    }

    [Fact]
    public void CanReachViaDetour_ReturnsTrueWhenEnoughCharge()
    {
        var ev = TestData.EV(battery: TestData.Battery(stateOfCharge: 0.2f));
        Assert.True(ev.CanReachViaDetour(15, 10, reserve: 0.1f));
    }

    [Fact]
    public void CanReachViaDetour_ReturnsFalseWhenNotEnoughCharge()
    {
        var ev = TestData.EV(battery: TestData.Battery(stateOfCharge: 0.2f));
        Assert.False(ev.CanReachViaDetour(1000, 10, reserve: 0.1f));
    }

    [Fact]
    public void CanReachViaDetour_DetourEqualsDirect_ZeroExtraCost()
    {
        var ev = TestData.EV(battery: TestData.Battery(stateOfCharge: 0.2f));
        Assert.True(ev.CanReachViaDetour(10, 10, reserve: 0.1f));
    }

    [Fact]
    public void CanReachViaDetour_MassiveDetour_ReturnsFalse()
    {
        var ev = TestData.EV(battery: TestData.Battery(stateOfCharge: 0.2f));
        Assert.False(ev.CanReachViaDetour(10000, 10, reserve: 0.1f));
    }

    [Fact]
    public void TimeToHalfBattery_BasicCalculation()
    {
        var ev = TestData.EV(battery: TestData.Battery(stateOfCharge: 0.5f));

        var result = ev.TimeToHalfBattery();

        Assert.Equal(166667u, result.Seconds);
    }

    [Fact]
    public void TimeToHalfBattery_SoCBelowMinAcceptable_UsesMinAcceptable()
    {
        var ev = TestData.EV(
            battery: TestData.Battery(stateOfCharge: 0.09f),
            preferences: TestData.Preferences(MinAcceptableCharge: 0.1f));

        var timeWithBelowAcceptableCharge = ev.TimeToHalfBattery();

        ev.Battery.StateOfCharge = 0.12f;

        var timeWithJustAboveMinimumCharge = ev.TimeToHalfBattery();

        Assert.Equal(timeWithBelowAcceptableCharge, timeWithJustAboveMinimumCharge);
    }

    [Fact]
    public void TimeToHalfBattery_SoCAboveMinAcceptable_UsesCurrentSoC()
    {
        var ev = TestData.EV(
            battery: TestData.Battery(stateOfCharge: 0.8f),
            preferences: TestData.Preferences(MinAcceptableCharge: 0.1f));

        var timeWithHighCharge = ev.TimeToHalfBattery();

        ev.Battery.StateOfCharge = 0.4f;

        var timeWithLowerCharge = ev.TimeToHalfBattery();

        Assert.True(timeWithHighCharge > timeWithLowerCharge);
    }
}
