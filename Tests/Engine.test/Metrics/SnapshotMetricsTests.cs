namespace Engine.test.Metrics;

using System;
using Core.Charging;
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
        _chargerA = ChargingTestFixtures.MakeSingleCharger(id: 1, maxPowerKW: 100);
        _chargerB = ChargingTestFixtures.MakeSingleCharger(id: 2, maxPowerKW: 100);

        _emptyStation = ChargingTestFixtures.MakeStation([]);
        _singleChargerStation = ChargingTestFixtures.MakeStation([_chargerA, _chargerB]);
        _dualChargerStation = ChargingTestFixtures.MakeStation([ChargingTestFixtures.MakeDualCharger(id: 1, maxPowerKW: 300)]);
    }

    [Fact]
    public void Collect_MaxUtilisation_WhenMaxDistribution()
    {
        var charger = ChargingTestFixtures.MakeSingleCharger(id: 1, maxPowerKW: 150);
        var station = ChargingTestFixtures.MakeStation([charger]);

        var metric = SnapshotMetric.Collect(station, 0, DayOfWeek.Monday, 0, _ => 150);

        Assert.Equal(1f, metric.Utilisation, precision: 3);
    }

    [Fact]
    public void Collect_PartialDelivery_UtilisationIsCorrect()
    {
        var charger = ChargingTestFixtures.MakeSingleCharger(id: 1, maxPowerKW: 300);
        var station = ChargingTestFixtures.MakeStation([charger]);

        var metric = SnapshotMetric.Collect(station, 0, DayOfWeek.Monday, 0, _ => 200);

        Assert.Equal(0.667f, metric.Utilisation, precision: 2);
    }

    [Fact]
    public void Collect_NoDelivery_UtilisationIsZero()
    {
        var metric = SnapshotMetric.Collect(_singleChargerStation, 0, DayOfWeek.Monday, 0, _ => 0);

        Assert.Equal(0f, metric.Utilisation);
    }

    [Fact]
    public void Collect_MultipleChargers_UtilisationIsAveraged()
    {
        var metric = SnapshotMetric.Collect(_singleChargerStation, 0, DayOfWeek.Monday, 0, charger => charger.Id == 1 ? 100 : 0);

        Assert.Equal(0.5f, metric.Utilisation, precision: 3);
    }

    [Fact]
    public void Collect_EmptyQueues_AvgQueueSizeIsZero()
    {
        var metric = SnapshotMetric.Collect(_singleChargerStation, 0, DayOfWeek.Monday, 0, _ => 0);

        Assert.Equal(0f, metric.AvgQueueSize);
    }

    [Fact]
    public void Collect_AvgQueueSizeIsCorrect()
    {
        var chargerA = ChargingTestFixtures.MakeSingleCharger(id: 1);
        var chargerB = ChargingTestFixtures.MakeSingleCharger(id: 2);
        chargerA.Queue.Enqueue(10);
        chargerA.Queue.Enqueue(11);

        var station = ChargingTestFixtures.MakeStation([chargerA, chargerB]);

        var metric = SnapshotMetric.Collect(station, 0, DayOfWeek.Monday, 0, _ => 0);

        Assert.Equal(1f, metric.AvgQueueSize, precision: 3);
    }

    [Theory]
    [InlineData(0, 0, 0f)]
    [InlineData(1, 0, 50f)]
    [InlineData(1, 1, 100f)]
    public void Collect_ActiveChargersPct_IsCorrect(int enqueuedOnA, int enqueuedOnB, float expectedPct)
    {
        var chargerA = ChargingTestFixtures.MakeSingleCharger(id: 1);
        var chargerB = ChargingTestFixtures.MakeSingleCharger(id: 2);
        for (var i = 0; i < enqueuedOnA; i++) chargerA.Queue.Enqueue(i);
        for (var i = 0; i < enqueuedOnB; i++) chargerB.Queue.Enqueue(i);
        var station = ChargingTestFixtures.MakeStation([chargerA, chargerB]);

        var metric = SnapshotMetric.Collect(station, 0, DayOfWeek.Monday, 0, _ => 0);

        Assert.Equal(expectedPct, metric.ActiveChargersPct, precision: 2);
    }

    [Fact]
    public void Collect_DualCharger_UtilisationIsCorrect()
    {
        var metric = SnapshotMetric.Collect(_dualChargerStation, 0, DayOfWeek.Monday, 0, _ => 150);

        Assert.Equal(0.5f, metric.Utilisation, precision: 3);
    }

    [Fact]
    public void Collect_DualChargerWithQueue_IsCountedAsActive()
    {
        var charger = ChargingTestFixtures.MakeDualCharger(id: 1);
        charger.Queue.Enqueue(99);
        var station = ChargingTestFixtures.MakeStation([charger]);

        var metric = SnapshotMetric.Collect(station, 0, DayOfWeek.Monday, 0, _ => 0);

        Assert.Equal(100f, metric.ActiveChargersPct, precision: 2);
    }

    [Fact]
    public void Collect_AvgPrice_IsWithinExpectedRange()
    {
        var metric = SnapshotMetric.Collect(_singleChargerStation, 0, DayOfWeek.Monday, 6, _ => 0);

        Assert.InRange(metric.AvgPrice, 2.40f, 3.60f);
    }
}