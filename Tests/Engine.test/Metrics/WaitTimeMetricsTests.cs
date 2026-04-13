namespace Engine.test.Metrics;

using Engine.Metrics.Events;
using Core.Shared;

public class WaitTimeMetricTests
{
    [Fact]
    public void WaitTime_Calculation_IsCorrect()
    {
        var arrival = new Time(100);
        var startCharge = new Time(250);

        var metric = new EVWaitTimeMetric
        {
            EVId = 1,
            StationId = 1,
            ArrivalAtStationTime = arrival,
            StartChargingTime = startCharge,
        };

        var result = metric.WaitTime;

        Assert.Equal(150u, result.Milliseconds);
    }
}