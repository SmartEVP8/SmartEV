namespace Testing;

using Engine.Spawning;

public class AliasSamplerTests
{
    [Fact]
    public void Sample_UniformWeights_ReturnsEachIndexEqually()
    {
        var sampler = new AliasSampler([1.0f, 1.0f, 1.0f, 1.0f]);
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
        var sampler = new AliasSampler([1.0f, 2.0f, 3.0f]);
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
        var sampler = new AliasSampler([1.0f]);
        var rng = new Random(42);

        for (var i = 0; i < 1.0f; i++)
            Assert.Equal(0, sampler.Sample(rng));
    }

    [Fact]
    public void RecreateOriginalProbabilities()
    {
        float[] weights = [0.1f, 0.2f, 0.3f, 0.4f];
        var sampler = new AliasSampler(weights);
        var orignalProbabilities = sampler.GetProbabilities();

        for (var i = 0; i < weights.Length; i++)
        {
            Assert.Equal(orignalProbabilities[i], weights[i]);
        }
    }

    [Fact]
    public void TotalProbabilitiesIs1()
    {
        float[] weights = [0.1f, 0.2f, 0.3f, 0.4f];
        var sampler = new AliasSampler(weights);
        var orignalProbabilities = sampler.GetProbabilities();

        var total = orignalProbabilities.Sum();
        Assert.Equal(1.0f, total);
    }


    [Fact]
    public void TotalProbabilitiesIs1LargeNumbers()
    {
        float[] weights = [200.0f, 300.0f, 500.0f];
        var sampler = new AliasSampler(weights);
        var orignalProbabilities = sampler.GetProbabilities();

        var total = orignalProbabilities.Sum();
        Assert.Equal(1.0f, total);
    }
}
