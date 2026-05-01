namespace Engine.test.Metrics;

using Core.Shared;
using Engine.test.Builders;
using Core.test.Builders;
using Engine.Metrics.Snapshots;
using Core.Charging;

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
        var stationDic = new Dictionary<ushort, Station> { [station.Id] = station };
        var stationService = EngineTestData.StationService(stationDic, new(), new());
        var collector = new StationMetricsCollector([station], stationService);

        var snapshotInterval = new Time(3600000);

        charger.Window = charger.Window with
        {
            DeliveredKWh = deliveredKwh,
            HadActivity = deliveredKwh > 0,
        };

        var (chargers, stations) = collector.Collect(snapshotInterval, new Time(3600));

        var cs = chargers.Single();

        Assert.Equal(expectedUtilization, cs.Utilization);
    }
}
