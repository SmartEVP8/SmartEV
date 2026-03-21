namespace Engine.Spawning;

using Engine.Grid;

/// <summary>
/// A shared store of the currently computed samplers.
/// </summary>
public class JourneySamplerProvider : IJourneySamplerProvider
{
    private readonly JourneyPipeline _pipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="JourneySamplerProvider"/> class.
    /// </summary>
    /// <param name="pipeline">JourneyPipeline computes the sampling distributions for source and destination points.</param>
    public JourneySamplerProvider(JourneyPipeline pipeline)
    {
        _pipeline = pipeline;
        const float defaultScalar = 1.0f;
        Current = _pipeline.Compute(defaultScalar);
    }

    /// <inheritdoc/>
    public IJourneySampler Current { get; private set; }

    /// <inheritdoc/>
    public IJourneySampler Recompute(float scalar)
    {
        Current = _pipeline.Compute(scalar);
        return Current;
    }
}
