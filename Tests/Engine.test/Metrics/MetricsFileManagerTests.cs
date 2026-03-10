using Engine.Metrics;
using Xunit;

namespace Engine.test.Metrics;

public class MetricsFileManagerTests
{
    private record TestType
    {
    }

    [Fact]
    public void FileInfo()
    {
        var guid = default(Guid);
        var path = Path.GetTempPath();
        var dir = new DirectoryInfo(path);
        var mfm = new MetricsFileManager(
                dir,
                guid);

        var fp = mfm.GetMetricPath<TestType>();
        var expectedFp = Path.Combine(path, guid.ToString(), $"{typeof(TestType).Name}.parquet");
        Assert.Equal(expectedFp, fp.ToString());

    }
}
