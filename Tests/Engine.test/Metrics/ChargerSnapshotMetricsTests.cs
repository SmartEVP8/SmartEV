namespace Engine.test.Metrics;

using Core.Shared;
using Engine.Metrics.Snapshots;
using Engine.test.Builders;

public class ChargerSnapshotMetricTests
{
    [Fact]
    public void Collect_CapturesProvidedChargerMetrics()
    {
        var charger1 = TestData.SingleCharger(id: 1, maxPowerKW: 50);
        var charger2 = TestData.SingleCharger(id: 2, maxPowerKW: 175);
        charger2.Queue.Enqueue(100);
        charger2.Queue.Enqueue(101);

        _ = TestData.Station(id: 7, chargers: [charger1, charger2]);

        var metric = ChargerSnapshotMetric.Collect(charger2, stationId: 7, simTime: new Time(3600), queueSize: charger2.Queue.Count, utilization: 0.73f);

        Assert.Equal(3600u, metric.SimTime);
        Assert.Equal(7, metric.StationId);
        Assert.Equal(2, metric.ChargerId);
        Assert.Equal(175f, metric.MaxKW);
        Assert.Equal(2, metric.QueueSize);
        Assert.Equal(0.73f, metric.Utilization);
    }
}
