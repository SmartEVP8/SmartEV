namespace Engine.Spawning;

using Engine.Grid;

/// <summary>
/// A shared store of the currently computed samplers.
/// </summary>
public class JourneySamplerProvider : IJourneySamplerProvider
{
    private readonly JourneyPipeline _pipeline;
    private readonly float _populationScalar;
    private readonly float _distanceScalar;

    /// <summary>
    /// Initializes a new instance of the <see cref="JourneySamplerProvider"/> class.
    /// </summary>
    /// <param name="pipeline">JourneyPipeline computes the sampling distributions for source and destination points.</param>
    /// <param name="populationScalar">Influence of city population on the gravity weight.</param>
    /// <param name="distanceScalar">Influence of distance on the gravity weight.</param>
    public JourneySamplerProvider(JourneyPipeline pipeline, float populationScalar, float distanceScalar)
    {
        _pipeline = pipeline;
        _populationScalar = populationScalar;
        _distanceScalar = distanceScalar;
        Current = _pipeline.Compute(_populationScalar, _distanceScalar);
    }

    /// <inheritdoc/>
    public IJourneySampler Current { get; private set; }

    /// <inheritdoc/>
    public IJourneySampler Recompute(float populationScalar, float distanceScalar)
    {
        Current = _pipeline.Compute(populationScalar, distanceScalar);
        return Current;
    }
}
