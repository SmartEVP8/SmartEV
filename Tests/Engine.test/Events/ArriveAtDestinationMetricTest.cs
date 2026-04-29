namespace Engine.test.Events;

using Engine.Metrics.Events;
using Core.Vehicles;
using Core.test.Builders;
using Core.Shared;

public class ArriveAtDestinationMetricTest
{
    [Fact]
    public void Collect_ExtractsAllMetricFields()
    {
        var departure = 100000U;
        var originalDuration = 50000U;
        var deviation = 12000;
        var simNow = (Time)(uint)(departure + originalDuration + deviation);

        var battery = CoreTestData.Battery();
        var preferences = CoreTestData.Preferences();

        var nextStop = new Position(1, 1);
        var route = new List<Position>
        {
            new(0, 0),
            nextStop,
            new(2, 2),
        };

        var journey = CoreTestData.Journey(
            waypoints: route,
            departure: departure,
            originalDuration: originalDuration);

        journey.UpdateRoute(
            route,
            nextStop,
            departure: departure,
            duration: 62000U,
            newDistanceKm: 10);

        var ev = new EV(1, battery, preferences, journey, 150);

        var metric = ArrivalAtDestinationMetric.Collect(ev, simNow);

        Assert.Equal(departure + originalDuration, metric.ExpectedArrivalTime);
        Assert.Equal(deviation, metric.PathDeviation);
    }

    [Fact]
    public void MissedDeadline_ComputedCorrectly()
    {
        var departure = 100000U;
        var originalDuration = 50000U;

        var battery = CoreTestData.Battery();
        var preferences = CoreTestData.Preferences();

        var route = new List<Position>
        {
            new(0, 0),
            new(1, 1),
        };

        var journey = CoreTestData.Journey(
            waypoints: route,
            departure: departure,
            originalDuration: originalDuration);

        var ev = new EV(1, battery, preferences, journey, 150);

        var deadline = DeadlineCalculator.Calculate(
            ev.Journey,
            ev.SpawnStateOfCharge,
            ev.Preferences.MinAcceptableCharge,
            (float)ev.Preferences.MaxPathDeviation,
            ev.Battery.MaxCapacityKWh,
            ev.EnergyForDistanceKWh(ev.Journey.Original.DistanceKm));

        var simNow = deadline + 1;

        var metric = ArrivalAtDestinationMetric.Collect(ev, simNow);

        Assert.True(metric.MissedDeadline);
        Assert.True(metric.DeltaArrivalTime > 0);
    }
}
