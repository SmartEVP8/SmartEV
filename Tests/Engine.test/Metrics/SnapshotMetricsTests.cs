namespace Engine.test.Metrics;

using System;
using Core.Charging;
using Core.Shared;
using Engine.Metrics;
using Xunit;

public class SnapshotMetricTests
{
    private readonly Station _emptyStation;
    private readonly Station _singleChargerStation;
    private readonly Station _dualChargerStation;
    private readonly SingleCharger _chargerA;
    private readonly SingleCharger _chargerB;

    public SnapshotMetricTests()
    {
        _chargerA = SnapshotMetricsHelper.MakeSingleCharger(id: 1, maxPowerKW: 100);
        _chargerB = SnapshotMetricsHelper.MakeSingleCharger(id: 2, maxPowerKW: 100);

        _emptyStation = SnapshotMetricsHelper.MakeStation([]);
        _singleChargerStation = SnapshotMetricsHelper.MakeStation([_chargerA, _chargerB]);
        _dualChargerStation = SnapshotMetricsHelper.MakeStation([SnapshotMetricsHelper.MakeDualCharger(id: 1, maxPowerKW: 300)]);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 2)]
    public void ActiveChargers_IsCorrect(int enqueuedOnA, int enqueuedOnB, int expectedActive)
    {
        var chargerA = SnapshotMetricsHelper.MakeSingleCharger(id: 1);
        var chargerB = SnapshotMetricsHelper.MakeSingleCharger(id: 2);
        for (var i = 0; i < enqueuedOnA; i++) chargerA.Queue.Enqueue(i);
        for (var i = 0; i < enqueuedOnB; i++) chargerB.Queue.Enqueue(i);
        var station = SnapshotMetricsHelper.MakeStation([chargerA, chargerB]);

        var metric = SnapshotMetric.Collect(station, 0, DayOfWeek.Monday, 0, _ => 0);

        Assert.Equal(expectedActive, metric.ActiveChargers);
    }

    [Fact]
    public void WithQueuedEVs_TotalQueueSizeIsCorrect()
    {
        var chargerA = SnapshotMetricsHelper.MakeSingleCharger(id: 1);
        var chargerB = SnapshotMetricsHelper.MakeSingleCharger(id: 2);
        chargerA.Queue.Enqueue(10);
        chargerA.Queue.Enqueue(11);
        
        var station = SnapshotMetricsHelper.MakeStation([chargerA, chargerB]);

        var metric = SnapshotMetric.Collect(station, 0, DayOfWeek.Monday, 0, _ => 0);

        Assert.Equal(2, metric.TotalQueueSize);
    }

    [Fact]
    public void TotalKW_IsCorrect()
    {
        var singleMetric = SnapshotMetric.Collect(_singleChargerStation, 0, DayOfWeek.Monday, 0, _ => 50);
        var dualMetric = SnapshotMetric.Collect(_dualChargerStation, 0, DayOfWeek.Monday, 0, _ => 150);

        Assert.Equal(100f, singleMetric.TotalDeliveredKW);
        Assert.Equal(200f, singleMetric.TotalMaxKW);
        Assert.Equal(150f, dualMetric.TotalDeliveredKW);
        Assert.Equal(300f, dualMetric.TotalMaxKW);
    }

    [Fact]
    public void Price_IsWithinExpectedRange()
    {
        // Base price is 3.00, deviation is ±10–20%, so valid range is [2.40, 3.60]
        var metric = SnapshotMetric.Collect(_singleChargerStation, 0, DayOfWeek.Monday, 6, _ => 0);

        Assert.InRange(metric.Price, 2.40f, 3.60f);
    }
}