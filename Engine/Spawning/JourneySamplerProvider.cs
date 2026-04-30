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
    private readonly float _populationScalar;
    private readonly float _distanceScalar;
    private readonly List<List<Position>> _wetPolygons;
    private Dictionary<uint, IJourneySampler> _cachedSamplers = new();

    private uint _lastJourneySamplerUpdateHour = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="JourneySamplerProvider"/> class.
    /// </summary>
    /// <param name="pipeline">JourneyPipeline computes the sampling distributions for source and destination points.</param>
    /// <param name="populationScalar">Influence of city population on the gravity weight.</param>
    /// <param name="distanceScalar">Influence of distance on the gravity weight.</param>
    /// <param name="wetPolygons">List of polygons representing wet areas where spawning is disallowed.</param>
    public JourneySamplerProvider(JourneyPipeline pipeline, float populationScalar, float distanceScalar, List<List<Position>> wetPolygons)
    {
        _pipeline = pipeline;
        _populationScalar = populationScalar;
        _distanceScalar = distanceScalar;
        _wetPolygons = wetPolygons;
        for (uint hour = 0; hour < 24; hour++)
        {
            var (popScalar, distScalar) = GetScalers(hour);
            _cachedSamplers[hour] = _pipeline.Compute(popScalar, distScalar, wetPolygons);
        }

        Current = _cachedSamplers[0];
    }

    /// <inheritdoc/>
    public IJourneySampler Current { get; private set; }

    /// <inheritdoc/>
    public IJourneySampler Recompute(Time time)
    {
        if (time.Hours == _lastJourneySamplerUpdateHour)
            return Current;

        _lastJourneySamplerUpdateHour = time.Hours;
        if (_cachedSamplers.TryGetValue(time.Hours, out var cachedSampler))
        {
            Current = cachedSampler;
            return Current;
        }

        Log.Error("No cached sampler found for hour {Hour}. This should not happen.", time.Hours);
        throw new InvalidOperationException($"No cached sampler found for hour {time.Hours}. This should not happen.");
    }

    private (float populationScaler, float distanceScaler) GetScalers(Time time)
    {
        const float baseScaler = 1f;
        const float maxVariance = 0.6f;

        var dailyFluctuation = (float)(maxVariance * Math.Sin((Math.PI * time.Hours) / 12));

        var populationScaler = baseScaler + dailyFluctuation;
        var distanceScaler = baseScaler - dailyFluctuation;

        return (populationScaler, distanceScaler);
    }
}
