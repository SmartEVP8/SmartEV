namespace Engine.Cost;

using Core.Helper;

/// <summary>
/// Enum for the different cost weight fields.
/// </summary>
public enum CostWeightField
{
    PriceSensitivity,
    PathDeviation,
    EffectiveQueueSize,
    Urgency,
    ExpectedWaitTime,
}

/// <summary>
/// Extension method for CostWeightField to convert enum values to user-friendly display names.
/// </summary>
public static class CostWeightFieldExtensions
{
    /// <summary>
    /// Converts a CostWeightField enum value to a user-friendly display name.
    /// </summary>
    /// <param name="field">The specific enum.</param>
    /// <returns>Pretty name :D.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If case isn't handled in the switch.</exception>
    public static string ToDisplayName(this CostWeightField field) => field switch
    {
        CostWeightField.PriceSensitivity => "Price Sensitivity",
        CostWeightField.PathDeviation => "Path Deviation",
        CostWeightField.EffectiveQueueSize => "Effective Queue Size",
        CostWeightField.ExpectedWaitTime => "Expected Wait Time",
        _ => throw Log.Error(0, 0, new ArgumentOutOfRangeException(nameof(field)))
    };
}

public record CostWeights(
    float PriceSensitivity = 0.4f,
    float PathDeviation = 0.8f,
    float EffectiveQueueSize = 1.0f,
    float ExpectedWaitTime = 1
);

public record WeightMetadata(int Id, float Min, float Max, string Name);

/// <summary>
/// Frontend initialization of the cost weight metadata. Used to validate and identify weights when updated from the frontend.
/// </summary>
public static class CostWeightMetadata
{
    /// <summary>
    /// Metadata for all supported cost weight fields.
    /// Used to validate and identify weights when they are updated from the frontend.
    /// </summary>
    public static readonly IReadOnlyDictionary<CostWeightField, WeightMetadata> All =
        new Dictionary<CostWeightField, WeightMetadata>
        {
            [CostWeightField.PriceSensitivity] = new(0, 0f, 1f, CostWeightField.PriceSensitivity.ToDisplayName()),
            [CostWeightField.PathDeviation] = new(1, 0f, 100f, CostWeightField.PathDeviation.ToDisplayName()),
            [CostWeightField.EffectiveQueueSize] = new(2, 0f, 100f, CostWeightField.EffectiveQueueSize.ToDisplayName()),
            [CostWeightField.ExpectedWaitTime] = new(4, 0f, 100f, CostWeightField.ExpectedWaitTime.ToDisplayName()),
        };
}
