namespace Core.Spawning;

/// <summary>
/// Implements Vose's Alias Method for efficient sampling from a discrete probability distribution.
/// https://cwyman.org/papers/rtg2-aliasMethod.pdf
/// </summary>
public class AliasSampler
{
    private readonly int[] _alias;
    private readonly double[] _probability;

    /// <summary>
    /// Initializes a new instance of the <see cref="AliasSampler"/> class.
    /// It constructs the alias table based on the provided weights.
    /// </summary>
    /// <param name="values">A list of weights that will be sampled from.</param>
    public AliasSampler(double[] values)
    {
        var length = values.Length;
        _alias = new int[length];
        _probability = new double[length];

        var avgWeight = values.Sum() / length;
        var weights = values.ToArray();

        var small = new PriorityQueue<int, double>();
        var large = new PriorityQueue<int, double>(Comparer<double>.Create((a, b) => b.CompareTo(a)));

        for (var i = 0; i < length; i++)
        {
            if (weights[i] < avgWeight) small.Enqueue(i, weights[i]);
            else large.Enqueue(i, weights[i]);
        }

        while (small.Count > 0 && large.Count > 0)
        {
            small.TryDequeue(out var s, out var sw);
            large.TryDequeue(out var l, out var lw);

            _probability[s] = sw / avgWeight;
            _alias[s] = l;

            var remaining = lw + sw - avgWeight;

            if (remaining < avgWeight) small.Enqueue(l, remaining);
            else large.Enqueue(l, remaining);
        }

        while (large.Count > 0) _probability[large.Dequeue()] = 1.0;
        while (small.Count > 0) _probability[small.Dequeue()] = 1.0;
    }

    /// <summary>
    /// Samples an index from the distribution defined by the weights provided in the constructor.
    /// </summary>
    /// <param name="rng">Random number generator to use for sampling. This allows for reproducibility if a seeded RNG is used. </param>
    /// <returns>The index of the sampled value, where the probability of each index being returned is proportional to the weight provided in the constructor. </returns>
    public int Sample(Random rng)
    {
        var bucketIndex = rng.Next(_alias.Length);
        var threshold = rng.NextDouble();
        return threshold < _probability[bucketIndex] ? bucketIndex : _alias[bucketIndex];
    }
}
