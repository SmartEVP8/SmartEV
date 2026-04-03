namespace Engine.test.Events;

using Engine.Metrics.Events;
using Core.Vehicles;
using Engine.test.Builders;
using Core.Shared;

public class ArriveAtDestinationMetricTest
{
    /// <summary>
    /// The Collect method should extract all required fields from the EV's journey and arrival time.
    /// </summary>
    [Fact]
    public void Collect_ExtractsAllMetricFields()
    {
        var departure = 100U;
        var originalDuration = 50U;
        var deviation = 12U;
        var simNow = (Time)(departure + originalDuration + deviation);

        var battery = TestData.Battery();
        var preferences = TestData.Preferences();
        var journey = TestData.Journey(waypoints: null, departure: 100U, originalDuration: 50U);
        journey.UpdateRoute(new Segments([]), new Position(0, 0), departure: 100, duration: 62U, newDistanceKm: 10);
        var ev = new EV(battery, preferences, journey, 150);

        var metric = ArrivalAtDestinationMetric.Collect(ref ev, simNow);

        Assert.Equal((Time)originalDuration, metric.ExpectedArrivalTime);
        Assert.Equal((Time)deviation, metric.PathDeviation);
    }

    /// <summary>
    /// MissedDeadline should be true when the EV accumulated deviation that pushed
    /// actual arrival past the expected duration.
    /// </summary>
    [Fact]
    public void MissedDeadline_ComputedCorrectly()
    {
        var departure = 100U;
        var originalDuration = 50U;
        var deviation = 12U;
        var simNow = (Time)(departure + originalDuration + deviation);

        var battery = TestData.Battery();
        var preferences = TestData.Preferences();
        var journey = TestData.Journey(waypoints: null, departure: departure, originalDuration: originalDuration);
        var ev = new EV(battery, preferences, journey, 150);

        var metric = ArrivalAtDestinationMetric.Collect(ref ev, simNow);

        Assert.True(metric.MissedDeadline);
        Assert.True(metric.DeltaArrivalTime > 0);
    }
}
