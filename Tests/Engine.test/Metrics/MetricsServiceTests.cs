namespace Engine.test.Metrics;

using Core.Shared;
using Engine.Metrics;
using Engine.Metrics.Events;
using Parquet.Serialization;

public class MetricsServiceIntegrationTests
{
    private readonly DirectoryInfo _dir = new(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));

    [Fact]
    public async Task RecordsAreWrittenToParquet()
    {
        var runId = Guid.NewGuid();
        var config = new MetricsConfig
        {
            BufferSize = 3,
            OutputDirectory = _dir,
            RecordEVWaitTimeInQueue = true,
        };

        await using (var service = new MetricsService(config, runId))
        {
            for (var i = 0; i < 5; i++)
            {
                service.RecordWaitTime(new WaitTimeInQueueMetric
                {
                    EVId = i,
                    StationId = 1,
                    ArrivalAtStationTime = new Time(0),
                    StartChargingTime = new Time(1),
                });
            }
        }

        var files = new MetricsFileManager(_dir, runId);
        var results = await ParquetSerializer.DeserializeAsync<WaitTimeRow>(files.GetMetricPath<WaitTimeInQueueMetric>().FullName);
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task DisabledMetricProducesNoFile()
    {
        var runId = Guid.NewGuid();
        var config = new MetricsConfig
        {
            BufferSize = 3,
            OutputDirectory = _dir,
            RecordEVWaitTimeInQueue = false,
        };

        await using (var service = new MetricsService(config, runId))
        {
            service.RecordWaitTime(new WaitTimeInQueueMetric
            {
                EVId = 1,
                StationId = 1,
                ArrivalAtStationTime = new Time(0),
                StartChargingTime = new Time(1),
            });
        }

        var files = new MetricsFileManager(_dir, runId);
        Assert.False(files.GetMetricPath<WaitTimeInQueueMetric>().Exists);
    }

    private sealed class WaitTimeRow
    {
        public int EVId { get; set; }

        public ushort StationId { get; set; }

        public Time ArrivalAtStationTime { get; set; }

        public Time StartChargingTime { get; set; }
    }
}
