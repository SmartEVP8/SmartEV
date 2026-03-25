namespace Engine.test.Metrics;

using Engine.Metrics;
using Parquet.Serialization;

public class MetricWriterTests
{
    public class TestMetric
    {
        public int Value { get; set; }
    }

    [Fact]
    public async Task FlushingFullBuffer()
    {
        var path = Path.GetTempFileName();
        const int count = 1;
        try
        {
            var writer = new MetricWriter<TestMetric>(count, new FileInfo(path));
            for (var i = 0; i < count; i++)
            {
                writer.Record(new TestMetric());
            }
            await writer.DisposeAsync();
            var results = await ParquetSerializer.DeserializeAsync<TestMetric>(path);
            Assert.Single(results);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DisposeWritesSlack()
    {
        var path = Path.GetTempFileName();
        const int count = 10;
        const int capacity = 8;

        try
        {
            var writer = new MetricWriter<TestMetric>(capacity, new FileInfo(path));
            for (var i = 0; i < count; i++)
            {
                writer.Record(new TestMetric());
            }
            await writer.DisposeAsync();
            var results = await ParquetSerializer.DeserializeAsync<TestMetric>(path);
            Assert.Equal(count, results.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
