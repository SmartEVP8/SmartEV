namespace Engine.test.Metrics;

using Engine.Metrics;
using Engine.Metrics.Snapshots;
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
            RecordCarSnapshots = true,
        };

        await using (var service = new MetricsService(config, runId))
        {
            for (var i = 0; i < 5; i++)
                service.RecordCar(new EVSnapshotMetric());
        }

        var files = new MetricsFileManager(_dir, runId);
        var results = await ParquetSerializer.DeserializeAsync<EVSnapshotMetric>(files.GetMetricPath<EVSnapshotMetric>().FullName);
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
            RecordCarSnapshots = false,
        };

        await using (var service = new MetricsService(config, runId))
        {
            service.RecordCar(new EVSnapshotMetric());
        }

        var files = new MetricsFileManager(_dir, runId);
        Assert.False(files.GetMetricPath<EVSnapshotMetric>().Exists);
    }
}
