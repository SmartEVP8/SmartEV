namespace API.EngineManager;

using Engine.Cost;

/// <summary>
/// Maps Cost DTO to domain objects.
/// </summary>
public static class CostWeightMapper
{
    /// <summary>
    /// Maps DTO to Domain.
    /// </summary>
    /// <param name="weights">DTO's.</param>
    /// <param name="current">Domain.</param>
    /// <returns>Domain cost weights with values from the DTO.</returns>
    public static CostWeights ToDomain(
        this IEnumerable<CostWeightDTO> weights,
        CostWeights current)
    {
        var dict = weights.ToDictionary(w => w.CostId, w => w.Value);

        return new CostWeights(
            PriceSensitivity: Get(dict, CostWeightField.PriceSensitivity, current.PriceSensitivity),
            PathDeviation: Get(dict, CostWeightField.PathDeviation, current.PathDeviation),
            ExpectedWaitTime: Get(dict, CostWeightField.ExpectedWaitTime, current.ExpectedWaitTime));
    }

    private static float Get(
        Dictionary<int, double> dict,
        CostWeightField field,
        float defaultValue)
    {
        var meta = CostWeightMetadata.All[field];

        if (!dict.TryGetValue(meta.Id, out var value))
            return defaultValue;

        return (float)value;
    }
}
