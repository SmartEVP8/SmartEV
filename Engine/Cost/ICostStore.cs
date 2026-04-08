namespace Engine.Cost;

/// <summary>
/// Defines an interface for a cost store that manages cost weights.
/// </summary>
public interface ICostStore
{
    /// <summary>
    /// Attempts to update a single weight field if the sequence number is greater than the last applied.
    /// </summary>
    /// <param name="field">The weight field to update.</param>
    /// <param name="value">The new value.</param>
    /// <param name="seq">The sequence number.</param>
    void TrySet(CostWeightField field, float value, long seq);

    /// <summary>
    /// Retrieves the current cost weights from the store.
    /// </summary>
    /// <returns> The current cost weights.</returns>
    CostWeights GetWeights();
}
