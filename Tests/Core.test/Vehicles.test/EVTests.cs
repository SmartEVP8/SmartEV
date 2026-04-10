namespace Core.test.Vehicles.test;

using Core.Routing;
using Core.Shared;
using Core.test.Builders;
using Core.Vehicles;

public class EVTests
{
    [Fact]
    public void Advance_UpdatesJourneyAndConsumesEnergy()
    {
        var battery = new Battery(100, 100, 1f);
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
        var battery = new Battery(100, 100, 0.5f);
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
        var battery = new Battery(0, 100, 0.5f);
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
        var battery = new Battery(50, 100, 0.5f);
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
        var battery = new Battery(100, 100, 0.5f);
        var preferences = new Preferences(1f, 0.15f, 10.0f);
        var ev = new EV(battery, preferences, journey, 150);

        var desiredSoC = ev.CalcDesiredSoC(100);

        Assert.Equal(0.16f, desiredSoC, precision: 2);
    }

    [Fact]
    public void CanCompleteJourney_ReturnsTrueWhenEnoughCharge()
    {
        var ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 1f));
        Assert.True(ev.CanCompleteJourney(minAcceptableCharge: 0.1f));
    }

    [Fact]
    public void CanCompleteJourney_ReturnsFalseWhenNotEnoughCharge()
    {
        var ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.001f));
        Assert.False(ev.CanCompleteJourney(minAcceptableCharge: 0.1f));
    }

    [Fact]
    public void CanCompleteJourney_ExactlyEnoughCharge_ReturnsTrue()
    {
        var ev = CoreTestData.EV(
            battery: CoreTestData.Battery(capacity: 100, stateOfCharge: 0.1002f),
            efficiency: 150);

        Assert.True(ev.CanCompleteJourney(minAcceptableCharge: 0.1f));
    }

    [Fact]
    public void CanReachViaDetour_ReturnsTrueWhenEnoughCharge()
    {
        var ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f));
        Assert.True(ev.CanReachViaDetour(15, 10, minAcceptableCharge: 0.1f));
    }

    [Fact]
    public void CanReachViaDetour_ReturnsFalseWhenNotEnoughCharge()
    {
        var ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f));
        Assert.False(ev.CanReachViaDetour(1000, 10, minAcceptableCharge: 0.1f));
    }

    [Fact]
    public void CanReachViaDetour_DetourEqualsDirect_ZeroExtraCost()
    {
        var ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f));
        Assert.True(ev.CanReachViaDetour(10, 10, minAcceptableCharge: 0.1f));
    }

    [Fact]
    public void CanReachViaDetour_MassiveDetour_ReturnsFalse()
    {
        var ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f));
        Assert.False(ev.CanReachViaDetour(10000, 10, minAcceptableCharge: 0.1f));
    }

    [Fact]
    public void TimeToNextCheck_HalfToNextStopWins()
    {
        var waypoints = new List<Position> { new(0, 0), new(0.1, 0) };
        var ev = CoreTestData.EV(
            waypoints: waypoints,
            departureTime: 0,
            originalDuration: 3600,
            battery: CoreTestData.Battery(stateOfCharge: 0.5f),
            efficiency: 150,
            distanceMeter: 100000f);

        var result = ev.TimeToNextFindCandidateCheck(0);

        Assert.Equal(1800u, result.Seconds);
    }

    [Fact]
    public void TimeToNextCheck_BatteryHalfWins()
    {
        var waypoints = new List<Position> { new(0, 0), new(0.1, 0) };
        var ev = CoreTestData.EV(
            waypoints: waypoints,
            departureTime: 0,
            originalDuration: 3600,
            battery: CoreTestData.Battery(capacity: 30, stateOfCharge: 0.2f),
            efficiency: 150,
            distanceMeter: 100000f);

        var result = ev.TimeToNextFindCandidateCheck(0);

        Assert.Equal(720u, result.Seconds);
    }

    [Fact]
    public void TimeToNextCheck_BothEqual()
    {
        var waypoints = new List<Position> { new(0, 0), new(0.1, 0) };
        var ev = CoreTestData.EV(
            waypoints: waypoints,
            departureTime: 0,
            originalDuration: 3600,
            battery: CoreTestData.Battery(stateOfCharge: 0.15f),
            efficiency: 150,
            distanceMeter: 100000f);

        var result = ev.TimeToNextFindCandidateCheck(0);

        Assert.Equal(1800u, result.Seconds);
    }

    [Fact]
    public void TimeToNextCheck_NonZeroDeparture()
    {
        var waypoints = new List<Position> { new(0, 0), new(0.1, 0) };
        var ev = CoreTestData.EV(
            waypoints: waypoints,
            departureTime: 1000,
            originalDuration: 3600,
            battery: CoreTestData.Battery(capacity: 30, stateOfCharge: 0.2f),
            efficiency: 150,
            distanceMeter: 100000f);

        var result = ev.TimeToNextFindCandidateCheck(1000);

        Assert.Equal(1720u, result.Seconds);
    }
}
