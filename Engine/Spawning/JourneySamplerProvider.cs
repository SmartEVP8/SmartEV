namespace Engine.Spawning;

using Engine.Grid;
using Core.Shared;
using Serilog;

/// <summary>
/// A shared store of the currently computed samplers.
/// </summary>
public class JourneySamplerProvider : IJourneySamplerProvider
{
    private readonly JourneyPipeline _pipeline;
    private readonly List<List<Position>> _wetPolygons;
    private readonly Dictionary<uint, IJourneySampler> _cachedSamplers = [];


    /// <summary>
    /// Initializes a new instance of the <see cref="JourneySamplerProvider"/> class.
    /// </summary>
    /// <param name="pipeline">JourneyPipeline computes the sampling distributions for source and destination points.</param>
    /// <param name="wetPolygons">List of polygons representing wet areas where spawning is disallowed.</param>
    public JourneySamplerProvider(JourneyPipeline pipeline, float PopulationScalar, float DistanceScalar, List<List<Position>> wetPolygons)
    {
        _pipeline = pipeline;
        _wetPolygons = wetPolygons;
        for (uint hour = 0; hour < 24; hour++)
        {
            var popScalar = GetScalers(hour);
            _cachedSamplers[hour] = _pipeline.Compute(popScalar, DistanceScalar, _wetPolygons);
        }

        Current = _cachedSamplers[0];
    }

    public IJourneySampler Current { get; private set; }

    /// <inheritdoc/>
    public IJourneySampler SetCurrent(Time time) => Current = _cachedSamplers[time.Hours];

    private float GetScalers(Time time)
    {
        const float baseScaler = 0.8f;
        const float maxVariance = 0.7f;

        var dailyFluctuation = (float)(maxVariance * Math.Sin((Math.PI * time.Hours) / 12));

        var populationScaler = baseScaler + dailyFluctuation;

        return populationScaler;
    }
}
