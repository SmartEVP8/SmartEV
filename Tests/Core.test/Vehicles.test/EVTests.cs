namespace Core.test.Vehicles.test;

using Core.Routing;
using Core.Shared;
using Core.Vehicles;

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
    public void CanCompleteJourney_ReturnsTrueWhenEnoughCharge()
    {
        var battery = new Battery(100, 100, 1f, Socket.CCS2);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(10, 0),
        };
        var journey = new Journey(departure: 0, duration: 100, distanceMeters: 10000, waypoints);
        var ev = new EV(battery, preferences, journey, 100);

        Assert.True(ev.CanCompleteJourney(reserve: 0.1f));
    }

    [Fact]
    public void CanCompleteJourney_ReturnsFalseWhenNotEnoughCharge()
    {
        var battery = new Battery(100, 100, 0.1f, Socket.CCS2);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(10, 0),
        };
        var journey = new Journey(departure: 0, duration: 100, distanceMeters: 10000, waypoints);
        var ev = new EV(battery, preferences, journey, 100);

        Assert.False(ev.CanCompleteJourney(reserve: 0.1f));
    }

    [Fact]
    public void CanReachDestination_ReturnsTrueWhenEnoughCharge()
    {
        var battery = new Battery(100, 100, 1f, Socket.CCS2);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(10, 0),
        };
        var journey = new Journey(departure: 0, duration: 100, distanceMeters: 10000, waypoints);
        var ev = new EV(battery, preferences, journey, 100);

        Assert.True(ev.CanReach(10, reserve: 0.1f));
    }

    [Fact]
    public void CanReachDestination_ReturnsFalseWhenNotEnoughCharge()
    {
        var battery = new Battery(100, 100, 0.1f, Socket.CCS2);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(10, 0),
        };
        var journey = new Journey(departure: 0, duration: 100, distanceMeters: 10000, waypoints);
        var ev = new EV(battery, preferences, journey, 100);

        Assert.False(ev.CanReach(10, reserve: 0.1f));
    }

    [Fact]
    public void CanReachViaDetour_ReturnsTrueWhenEnoughCharge()
    {
        var battery = new Battery(100, 100, 0.2f, Socket.CCS2);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(5, 0),
            new(10, 0),
        };
        var journey = new Journey(departure: 0, duration: 100, distanceMeters: 10000, waypoints);
        var ev = new EV(battery, preferences, journey, 100);

        Assert.True(ev.CanReachViaDetour(15, 10, reserve: 0.1f));
    }

    [Fact]
    public void CanReachViaDetour_ReturnsFalseWhenNotEnoughCharge()
    {
        var battery = new Battery(100, 100, 0.2f, Socket.CCS2);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(5, 0),
            new(10, 0),
        };
        var journey = new Journey(departure: 0, duration: 100, distanceMeters: 10000, waypoints);
        var ev = new EV(battery, preferences, journey, 100);

        Assert.False(ev.CanReachViaDetour(1000, 10, reserve: 0.1f));
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
    public void CalcDesiredSoC_ZeroDurationJourney_ReturnsFiniteClampedValue()
    {
        var battery = new Battery(100, 100, 0.5f, Socket.CCS2);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(1, 0),
        };

        // A degenerate journey can occur after reroute math/tolerance matching and must never produce NaN/Infinity target SoC.
        var journey = new Journey(departure: 100, duration: 0, distanceMeters: 1_000, waypoints);
        var ev = new EV(battery, preferences, journey, 150);

        var desired = ev.CalcDesiredSoC(arrivalAtStation: 100);

        Assert.False(float.IsNaN(desired));
        Assert.False(float.IsInfinity(desired));
        Assert.InRange(desired, 0f, 1f);
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

}
