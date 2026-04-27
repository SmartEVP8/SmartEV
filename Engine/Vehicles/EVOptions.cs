namespace Engine.Vehicles;

public record EVOptions
{
    /// <summary>
    /// GetsDistribution of starting state of charge (SoC) for created EVs. Keys are the discrete SoC values, and values are the probabilities of each SoC being sampled.
    /// </summary>
    public IReadOnlyDictionary<float, double> StartSoCDistribution { get; init; }
        = new Dictionary<float, double>
        {
            { 0.0f, 0.0 },
            { 0.10f, 0.0 },
            { 0.20f, 0.0 },
            { 0.30f, 0.09 },
            { 0.40f, 0.12 },
            { 0.50f, 0.15 },
            { 0.60f, 0.17 },
            { 0.70f, 0.21 },
            { 0.80f, 0.23 },
            { 0.90f, 0.02 },
            { 1.00f, 0.01 },
        };
}
