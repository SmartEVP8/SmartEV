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
        var journey = new Journey(departure: 0, duration: 100000, distanceMeters: 10_000, waypoints);
        var ev = new EV(battery, preferences, journey, 100);

        var currentPosition = ev.Advance(50000);

        Assert.Equal(new Position(5, 0), currentPosition);
        Assert.Equal(50000u, ev.Journey.Current.Departure.Milliseconds);
        Assert.InRange(ev.Journey.Current.DistanceKm, 4.999f, 5.001f);
        Assert.InRange(ev.Battery.StateOfCharge, 0.9949f, 0.9951f);
    }

    [Fact]
    public void Advance_MultipleTimesForSameTimestampIsSafe()
    {
        var battery = new Battery(100, 100, 1f);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var waypoints = new List<Position>
        {
            new(0, 0),
            new(10, 0),
        };
        var journey = new Journey(departure: 0, duration: 100000, distanceMeters: 10_000, waypoints);
        var ev = new EV(battery, preferences, journey, 100);

        ev.Advance(50000);

        var firstSoc = ev.Battery.StateOfCharge;

        Assert.Equal(50000u, ev.Journey.Current.Departure.Milliseconds);

        ev.Advance(50000);
        var secondSoc = ev.Battery.StateOfCharge;

        Assert.Equal(50000u, ev.Journey.Current.Departure.Milliseconds);
        Assert.Equal(firstSoc, secondSoc);
    }

    [Fact]
    public void EstimateSoCAtNextStop_MatchesActualSoCAfterAdvance()
    {
        var battery1 = new Battery(100, 100, 1f);
        var battery2 = new Battery(100, 100, 1f);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var waypoints = new List<Position>
    {
        new(0, 0),
        new(10, 0),
    };
        var journey1 = new Journey(departure: 0, duration: 100000, distanceMeters: 10_000, waypoints);
        var journey2 = new Journey(departure: 0, duration: 100000, distanceMeters: 10_000, waypoints);
        var ev1 = new EV(battery1, preferences, journey1, 100);
        var ev2 = new EV(battery2, preferences, journey2, 100);

        // Estimate SoC at next stop for ev1 (without advancing)
        var estimatedSoC = ev1.EstimateSoCAtNextStop();

        // Actually advance ev2 to the next stop
        ev2.Advance(ev2.Journey.Current.Departure.Milliseconds + ev2.Journey.Current.Duration);
        var actualSoC = ev2.Battery.StateOfCharge;

        Assert.InRange(estimatedSoC, actualSoC - 0.0001f, actualSoC + 0.0001f);
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
        var journey = new Journey(departure: 0, duration: 25200000, distanceMeters: 400000, waypoints);
        journey.UpdateRoute(waypoints, waypoints[1], departure: 0, duration: 25200000, newDistanceKm: 400);
        var ev = new EV(battery, preferences, journey, 100);

        var desiredSoC = ev.CalcDesiredSoC(12600000);

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
        var journey = new Journey(departure: 0, duration: 100000, distanceMeters: 1_000, waypoints);
        var ev = new EV(battery, preferences, journey, 150);

        var ex = Assert.Throws<InvalidOperationException>(() => ev.CalcDesiredSoC(arrivalAtStation: 10000));
        Assert.Contains("Battery capacity", ex.Message);
    }

    [Fact]
    public void CalcDesiredSoC_CannotReachWithSingleCharge_CapsAt80Percent()
    {
        // Huge remaining distance → energyToDest exceeds battery capacity
        var waypoints = new List<Position> { new(0, 0), new(10, 0) };
        var journey = new Journey(departure: 0, duration: 25200000, distanceMeters: 2_000_000, waypoints);
        journey.UpdateRoute(waypoints, waypoints[^1], 0, 25200000, 2000f);
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
        var journey = new Journey(departure: 0, duration: 100000, distanceMeters: 1000, waypoints);
        var battery = new Battery(100, 100, 0.5f);
        var preferences = new Preferences(1f, 0.15f, 10.0f);
        var ev = new EV(battery, preferences, journey, 150);

        var desiredSoC = ev.CalcDesiredSoC(100000);

        Assert.Equal(0.16f, desiredSoC, precision: 2);
    }

    [Fact]
    public void Advance_ConsumesExpectedEnergy_ForGivenDistance()
    {
        var battery = new Battery(100, 100, 1.0f); // 100 kWh, 100 kW, 100% SoC
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var waypoints = new List<Position> { new(0, 0), new(10, 0) };
        var journey = new Journey(departure: 0, duration: 100000, distanceMeters: 10_000, waypoints);
        ushort efficiency = 200; // 200 Wh/km

        var ev = new EV(battery, preferences, journey, efficiency);

        ev.Advance(100000);

        var expectedSoC = 1.0f - (2.0f / 100.0f); // 2 kWh used from 100 kWh

        Assert.InRange(ev.Battery.StateOfCharge, expectedSoC - 0.0001f, expectedSoC + 0.0001f);
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
    public void CanReachToStation_ReturnsTrueWhenEnoughCharge()
    {
        var ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f));
        Assert.True(ev.CanReachToStation(5, minAcceptableCharge: 0.1f));
    }

    [Fact]
    public void CanReachToStation_ReturnsFalseWhenNotEnoughCharge()
    {
        var ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f));
        Assert.False(ev.CanReachToStation(1000, minAcceptableCharge: 0.1f));
    }

    [Fact]
    public void CanReachToStation_MassiveDistance_ReturnsFalse()
    {
        var ev = CoreTestData.EV(battery: CoreTestData.Battery(stateOfCharge: 0.2f));
        Assert.False(ev.CanReachToStation(10000, minAcceptableCharge: 0.1f));
    }

    [Fact]
    public void TimeToNextCheck_HalfToNextStopWins()
    {
        var waypoints = new List<Position> { new(0, 0), new(0.1, 0) };
        var ev = CoreTestData.EV(
            waypoints: waypoints,
            departureTime: 0,
            originalDuration: 3600000,
            battery: CoreTestData.Battery(stateOfCharge: 0.5f),
            efficiency: 150,
            distanceMeter: 100000f);

        var result = ev.TimeAtNextFindCandidateCheck(0);

        Assert.Equal(1800u, result.TotalSeconds);
    }

    [Fact]
    public void TimeToNextCheck_BatteryHalfWins()
    {
        var waypoints = new List<Position> { new(0, 0), new(0.1, 0) };
        var ev = CoreTestData.EV(
            waypoints: waypoints,
            departureTime: 0,
            originalDuration: 3600000,
            battery: CoreTestData.Battery(capacity: 30, stateOfCharge: 0.2f),
            efficiency: 150,
            distanceMeter: 100000f);

        var result = ev.TimeAtNextFindCandidateCheck(0);

        Assert.Equal(720u, result.TotalSeconds);
    }

    [Fact]
    public void TimeToNextCheck_BothEqual()
    {
        var waypoints = new List<Position> { new(0, 0), new(0.1, 0) };
        var ev = CoreTestData.EV(
            waypoints: waypoints,
            departureTime: 0,
            originalDuration: 3600000,
            battery: CoreTestData.Battery(stateOfCharge: 0.15f),
            efficiency: 150,
            distanceMeter: 100000f);

        var result = ev.TimeAtNextFindCandidateCheck(0);

        Assert.Equal(1800u, result.TotalSeconds);
    }

    [Fact]
    public void TimeToNextCheck_NonZeroDeparture()
    {
        var waypoints = new List<Position> { new(0, 0), new(0.1, 0) };
        var ev = CoreTestData.EV(
            waypoints: waypoints,
            departureTime: 1000,
            originalDuration: 3600000,
            battery: CoreTestData.Battery(capacity: 30, stateOfCharge: 0.2f),
            efficiency: 150,
            distanceMeter: 100000f);

        var result = ev.TimeAtNextFindCandidateCheck(1000);

        Assert.Equal(721u, result.TotalSeconds);
    }

    [Fact]
    public void CalcPreComputedDesiredSoC_ReturnsExpectedValue()
    {
        var waypoints = new List<Position> { new(0, 0), new(45, 0), new(90, 0) };
        var journey = new Journey(departure: 0, duration: 5400, distanceMeters: 90, waypoints);
        var battery = new Battery(75, 150, 0.5f);
        var preferences = new Preferences(1f, 0.15f, 10.0f);
        var ev = new EV(battery, preferences, journey, 180);

        var distanceToDestinationKm = 45f;
        var preComputedDesiredSoC = ev.PreCalculatedTargetSoC(distanceToDestinationKm);

        Assert.Equal(0.27f, preComputedDesiredSoC, precision: 2);
    }

    [Fact]
    public void EstimateSoCAfterDuration()
    {
        var waypoints = new List<Position> { new(0, 0), new(10, 0) };
        var journey = new Journey(departure: 0, duration: 3600, distanceMeters: 10000, waypoints);
        var battery = new Battery(100, 100, 1f);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var ev = new EV(battery, preferences, journey, 200);

        var estimatedSoC = ev.EstimateSoCAfterADuration(1800000);

        Assert.InRange(estimatedSoC, 0.9f, 0.91f);
    }

    [Fact]
    public void EstimateSoCAfterDuration_ZeroDuration_ReturnsCurrentSoC()
    {
        var waypoints = new List<Position> { new(0, 0), new(10, 0) };
        var journey = new Journey(departure: 0, duration: 3600, distanceMeters: 10000, waypoints);
        var battery = new Battery(100, 100, 0.5f);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var ev = new EV(battery, preferences, journey, 200);

        var estimatedSoC = ev.EstimateSoCAfterADuration(0);
        Assert.InRange(estimatedSoC, 0.499f, 0.501f);
    }

    [Fact]
    public void DistanceEVCanDriveInTime()
    {
        var waypoints = new List<Position> { new(0, 0), new(10, 0) };
        var journey = new Journey(departure: 0, duration: 3600, distanceMeters: 10000, waypoints);
        var battery = new Battery(100, 100, 1f);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var ev = new EV(battery, preferences, journey, 200);

        var distanceInTime = ev.DistanceEVCanDrive(1800000);

        Assert.Equal(5000f, distanceInTime);
    }

    [Fact]
    public void DistanceEVCanDriveInTime_ZeroDuration_ReturnsZero()
    {
        var waypoints = new List<Position> { new(0, 0), new(10, 0) };
        var journey = new Journey(departure: 0, duration: 3600, distanceMeters: 10000, waypoints);
        var battery = new Battery(100, 100, 1f);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var ev = new EV(battery, preferences, journey, 200);

        var distanceInTime = ev.DistanceEVCanDrive(0);

        Assert.Equal(0f, distanceInTime);
    }

    [Fact]
    public void SoCUsedAfterADistance_ReturnsExpectedValue()
    {
        var waypoints = new List<Position> { new(0, 0), new(10, 0) };
        var journey = new Journey(departure: 0, duration: 3600, distanceMeters: 10000, waypoints);
        var battery = new Battery(100, 100, 1f);
        var preferences = new Preferences(1f, 0.1f, 10.0f);
        var ev = new EV(battery, preferences, journey, 200);

        var socUsed = ev.SoCUsedAfterADistance(300);

        Assert.InRange(socUsed, 0.4f, 0.41f);
    }

    [Fact]
    public void CheckIfTargetSoCIsLowerThatCurrentSoC_ReturnsTrue_WhenTargetSoCIsLower()
    {
        var ev = CoreTestData.EV(
            waypoints: new List<Position> { new(0, 0), new(1, 1) },
            battery: CoreTestData.Battery(capacity: 100, maxChargeRate: 150, stateOfCharge: 0.5f));

        var result = ev.CheckIfTargetSoCIsLowerThanCurrentSoC(10, 100, 0.9f);
        Assert.True(result);
    }

    [Fact]
    public void CheckIfTargetSoCIsLowerThatCurrentSoC_ReturnsFalse_WhenTargetSoCIsHigher()
    {
        var ev = CoreTestData.EV(
            waypoints: new List<Position> { new(0, 0), new(1, 1) },
            battery: CoreTestData.Battery(capacity: 100, maxChargeRate: 150, stateOfCharge: 0.5f));

        var result = ev.CheckIfTargetSoCIsLowerThanCurrentSoC(2, 100000000, 1f);
        Assert.False(result);
    }
}
