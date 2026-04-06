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
}
