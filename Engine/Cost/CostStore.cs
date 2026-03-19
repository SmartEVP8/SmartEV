namespace Engine.Cost;

/// <summary>
/// Initializes a new instance of the <see cref="CostStore"/> class.
/// </summary>
/// <param name="initialState">The inital weight configuration.</param>
public class CostStore(CostWeights initialState) : ICostStore
{
    private readonly Lock _lock = new();
    private long _lastSeq = -1;
    private CostWeights _state = initialState;

    /// <summary>
    /// Attempts to update the cost weights if the provided sequence number is greater than the last applied sequence number.
    /// <para>
    /// Intended usage:
    /// var current = store.Get();
    /// store.TrySet(current with { SliderA = 0.75 }, seq);
    /// </para>
    /// </summary>
    /// <param name="update">The updated state.</param>
    /// <param name="seq">The sequence number. TrySet only updates if seq is larger than previously set.</param>
    public void TrySet(CostWeights update, long seq)
    {
        lock (_lock)
        {
            if (seq <= _lastSeq)
                return;

            _state = update;
            _lastSeq = seq;
        }
    }

    /// <summary>
    /// Gets the current weights.
    /// </summary>
    /// <returns>The weights for cost.</returns>
    public CostWeights GetWeights()
    {
        lock (_lock)
        {
            return _state;
        }
    }
}
