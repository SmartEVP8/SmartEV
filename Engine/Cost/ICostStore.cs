namespace Engine.Cost;

/// <summary>
/// Defines an interface for a cost store that manages cost weights.
/// </summary>
public interface ICostStore
{
    /// <summary>
    /// Attempts to update the cost weights with the provided update and sequence number.
    /// </summary>
    /// <param name="update">The cost weights to update with.</param>
    /// <param name="seq">The sequence number for the update.</param>
    void TrySet(CostWeights update, long seq);

    /// <summary>
    /// Retrieves the current cost weights from the store.
    /// </summary>
    /// <returns> The current cost weights.</returns>
    CostWeights GetWeights();
}
