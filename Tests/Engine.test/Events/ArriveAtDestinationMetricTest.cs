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
        var simNow = (Time)(departure + originalDuration + deviation);

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

        var ev = new EV(battery, preferences, journey, 150);

        var metric = ArrivalAtDestinationMetric.Collect(ref ev, simNow);

        Assert.Equal(originalDuration, metric.ExpectedArrivalTime);
        Assert.Equal(deviation, metric.PathDeviation);
    }

    [Fact]
    public void MissedDeadline_ComputedCorrectly()
    {
        var departure = 100000U;
        var originalDuration = 50000U;
        var deviation = 12000U;
        var simNow = (Time)(departure + originalDuration + deviation);

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

        var ev = new EV(battery, preferences, journey, 150);

        var metric = ArrivalAtDestinationMetric.Collect(ref ev, simNow);

        Assert.True(metric.MissedDeadline);
        Assert.True(metric.DeltaArrivalTime > 0);
    }
}