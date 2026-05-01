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
        Parallel.For(0, 24, hour =>
        {
            var (popScalar, distScalar) = GetScalers((uint)hour, PopulationScalar, DistanceScalar);
            _cachedSamplers[(uint)hour] = _pipeline.Compute(popScalar, distScalar, _wetPolygons);
        });
    }

    /// <inheritdoc/>
    public IJourneySampler GetCurrent(Time time) => _cachedSamplers[time.Hours];

    private (float populationScaler, float distanceScaler) GetScalers(Time time, float PopulationScalar, float DistanceScalar)
    {
        const float baseScaler = 7f;
        const float maxVariance = 0.7f;

        var dailyFluctuation = (float)(maxVariance * Math.Sin((Math.PI * time.Hours) / 12));

        var populationScaler = baseScaler + dailyFluctuation;
        var distanceScaler = (baseScaler - dailyFluctuation) * DistanceScalar;

        return (populationScaler, DistanceScalar);
    }
}
