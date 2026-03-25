namespace Engine.test.Metrics;

using System;
using Xunit;
using Engine.test.Builders;
using Engine.Metrics.Snapshots;

public class SnapshotMetricTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 2)]
    public void ActiveChargers_IsCorrect(int enqueuedOnA, int enqueuedOnB, int expectedActive)
    {
        var chargerA = TestData.MakeSingleCharger(id: 1);
        var chargerB = TestData.MakeSingleCharger(id: 2);
        for (var i = 0; i < enqueuedOnA; i++) chargerA.Queue.Enqueue(i);
        for (var i = 0; i < enqueuedOnB; i++) chargerB.Queue.Enqueue(i);
        var station = TestData.Station(1, chargers: [chargerA, chargerB]);

        var metric = SnapshotMetric.Collect(station, 0, DayOfWeek.Monday, 0, _ => 0);

        Assert.Equal(expectedActive, metric.ActiveChargers);
    }
}
