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
        const int COUNT = 1;
        try
        {
            var writer = new MetricWriter<TestMetric>(COUNT, new FileInfo(path));
            for (var i = 0; i < COUNT; i++)
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
        const int COUNT = 10;
        const int CAPACITY = 8;
        try
        {
            var writer = new MetricWriter<TestMetric>(CAPACITY, new FileInfo(path));
            for (var i = 0; i < COUNT; i++)
            {
                writer.Record(new TestMetric());
            }

            await writer.DisposeAsync();
            var results = await ParquetSerializer.DeserializeAsync<TestMetric>(path);
            Assert.Equal(COUNT, results.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
