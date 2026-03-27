namespace Testing;

using Engine.Metrics.Events;
using Core.Vehicles;
using Engine.test.Builders;
using Core.Shared;

public class DeadlineMetricTest
{
    /// <summary>
    /// The Collect method should extract all required fields from the EV's journey and arrival time.
    /// </summary>
    [Fact]
    public void Collect_ExtractsAllMetricFields()
    {
        var departure = 100U;
        var originalDuration = 50U;
        var expectedDeadline = departure + originalDuration;
        var simNow = expectedDeadline + 5U;

        var battery = TestData.Battery();
        var preferences = TestData.Preferences();
        var journey = TestData.Journey(waypoints: null, departure: 100U, originalDuration: 50U);
        journey.UpdateRoute(new Paths([]), departure: 100, duration: 62U);
        var ev = new EV(battery, preferences, journey, 150);

        var metric = DeadlineMetric.Collect(ref ev, simNow);

        Assert.True(metric.ExpectedDeadline == expectedDeadline);
        Assert.True(metric.ActualArrivalTime == simNow);
        Assert.Equal(12U, metric.PathDeviation);
    }

    /// <summary>
    /// MissedDeadline should be true when arrival is after the deadline (DeltaDeadline > 0)
    /// and false when arrival is on/before deadline (DeltaDeadline ≤ 0).
    /// </summary>
    [Fact]
    public void MissedDeadline_ComputedCorrectly()
    {
        var departure = 100U;
        var originalDuration = 50U;
        var expectedDeadline = departure + originalDuration;
        var simNow = expectedDeadline + 1U;

        var battery = TestData.Battery();
        var preferences = TestData.Preferences();
        var journey = TestData.Journey(waypoints: null, departure: 100U, originalDuration: 50U);
        var ev = new EV(battery, preferences, journey, 150);

        var metric = DeadlineMetric.Collect(ref ev, simNow);

        Assert.True(metric.MissedDeadline);
        Assert.True(metric.DeltaDeadline > 0U);
    }
}
