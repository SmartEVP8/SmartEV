namespace Testing;

using Core.Spawning;


public class AliasSamplerTests
{
    [Fact]
    public void Sample_UniformWeights_ReturnsEachIndexEqually()
    {
        var sampler = new AliasSampler([1.0, 1.0, 1.0, 1.0]);
        var counts = new int[4];
        var rng = new Random(42);

        for (var i = 0; i < 100_000; i++)
            counts[sampler.Sample(rng)]++;

        // Each bucket should be ~25% ± 1%
        foreach (var count in counts)
            Assert.InRange(count, 24_000, 26_000);
    }

    [Fact]
    public void Sample_SkewedWeights_RespectsRelativeWeights()
    {
        // Index 2 has 3x the weight of index 0
        var sampler = new AliasSampler([1.0, 2.0, 3.0]);
        var counts = new int[3];
        var rng = new Random(42);

        for (var i = 0; i < 600_000; i++)
            counts[sampler.Sample(rng)]++;

        Assert.InRange(counts[0], 95_000, 105_000);  // ~1/6
        Assert.InRange(counts[1], 195_000, 205_000); // ~2/6
        Assert.InRange(counts[2], 295_000, 305_000); // ~3/6
    }

    [Fact]
    public void Sample_SingleWeight_AlwaysReturnsSameIndex()
    {
        var sampler = new AliasSampler([1.0]);
        var rng = new Random(42);

        for (var i = 0; i < 100; i++)
            Assert.Equal(0, sampler.Sample(rng));
    }
}
