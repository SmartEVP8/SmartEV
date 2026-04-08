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

    /// <inheritdoc/>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if value is outside the allowed range.</exception>
    public void TrySet(CostWeightField field, float value, long seq)
    {
        var meta = CostWeightMetadata.All[field];

        if (value < meta.Min || value > meta.Max)
            throw new ArgumentOutOfRangeException(nameof(value), $"{meta.Id}: {value} outside [{meta.Min}, {meta.Max}]");

        lock (_lock)
        {
            if (seq <= _lastSeq)
                return;

            _state = field switch
            {
                CostWeightField.PriceSensitivity => _state with { PriceSensitivity = value },
                CostWeightField.PathDeviation => _state with { PathDeviation = value },
                CostWeightField.EffectiveQueueSize => _state with { EffectiveQueueSize = value },
                CostWeightField.Urgency => _state with { Urgency = value },
                CostWeightField.ExpectedWaitTime => _state with { ExpectedWaitTime = value },
                _ => throw new ArgumentOutOfRangeException($"Field {nameof(field)} is not handled in switch")
            };

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
