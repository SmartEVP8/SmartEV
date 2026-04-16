namespace Engine.test.Metrics;

using Core.Shared;
using Engine.test.Builders;
using Core.test.Builders;
using Engine.Metrics.Snapshots;
public class StationServiceSnapshotTests
{
    [Theory]
    [InlineData(120, 60.0, 0.5f)] // 120kW charger, 60kWh delivered in 1 hr = 50% utilization
    [InlineData(150, 150.0, 1.0f)] // 150kW charger, 150kWh delivered in 1 hr = 100% utilization
    [InlineData(50, 0.0, 0.0f)] // 50kW charger, 0kWh delivered in 1 hr = 0% utilization
    public void Collect_CalculatesUtilizationAndAggregatesCorrectly(
            ushort chargerMaxKw,
            double deliveredKwh,
            float expectedUtilization)
    {
        var charger = CoreTestData.SingleCharger(1, maxPowerKW: chargerMaxKw);
        var station = CoreTestData.Station(1, chargers: [charger]);
        var collector = new StationMetricsCollector([station]);

        var expectedQueueSize = 2;
        var snapshotInterval = new Time(3600000);

        charger.Window = charger.Window with
        {
            DeliveredKWh = deliveredKwh,
            HadActivity = deliveredKwh > 0,
        };

        for (var i = 0; i < expectedQueueSize; i++)
        {
            charger.Queue.Enqueue((i, EngineTestData.ConnectedEV(i, 0.2, 0.8)));
        }

        var (chargers, stations) = collector.Collect(snapshotInterval, new Time(3600));

        var cs = chargers.Single();
        var ss = stations.Single();

        Assert.Equal(deliveredKwh, cs.DeliveredKW);
        Assert.Equal(expectedUtilization, cs.Utilization);
        Assert.Equal(expectedQueueSize, cs.QueueSize);

        Assert.Equal(deliveredKwh, ss.TotalDeliveredKWh);
        Assert.Equal(chargerMaxKw, ss.TotalMaxKWh);
        Assert.Equal(expectedQueueSize, ss.TotalQueueSize);
    }
}
