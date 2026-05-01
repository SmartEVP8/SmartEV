namespace Engine.Spawning;

using Engine.Grid;
using Core.Shared;

/// <summary>
/// A shared store of the currently computed samplers.
/// </summary>
public sealed class JourneySamplerProvider : IJourneySamplerProvider
{
    private readonly JourneyPipeline _pipeline;
    private readonly List<List<Position>> _wetPolygons;
    private readonly float _distanceScalar;

    /// <summary>
    /// Initializes a new instance of the <see cref="JourneySamplerProvider"/> class.
    /// </summary>
    /// <param name="pipeline">The journey pipeline used to compute sampler data.</param>
    /// <param name="distanceScalar">The distance scaling factor.</param>
    /// <param name="wetPolygons">The wet polygon definitions used during sampling.</param>
    public JourneySamplerProvider(
        JourneyPipeline pipeline,
        float distanceScalar,
        List<List<Position>> wetPolygons)
    {
        _pipeline = pipeline;
        _wetPolygons = wetPolygons;
        _distanceScalar = distanceScalar;

        JourneySamplerCache.EnsureDirectory();

        Parallel.For(0, 24, hour => EnsureSamplerOnDisk((uint)hour));

        Current = LoadHourFromDisk(0);
    }

    /// <inheritdoc/>
    public IJourneySampler Current { get; private set; }

    /// <inheritdoc/>
    public void SetCurrent(Time time) => Current = LoadHourFromDisk(time.Hours);

    private void EnsureSamplerOnDisk(uint hour)
    {
        if (JourneySamplerCache.Exists(hour)) return;

        var popScalar = GetScalers(hour);
        var dto = _pipeline.ComputeDto(popScalar, _distanceScalar, _wetPolygons);
        JourneySamplerCache.Write(hour, dto);
    }

    private JourneySamplers LoadHourFromDisk(uint hour)
    {
        var dto = JourneySamplerCache.Read(hour);
        return JourneyPipeline.FromDto(dto);
    }

    private float GetScalers(Time time)
    {
        const float baseScaler = 0.8f;
        const float maxVariance = 0.7f;

        var dailyFluctuation = (float)(maxVariance * Math.Sin((Math.PI * time.Hours) / 12));

        var populationScaler = baseScaler + dailyFluctuation;

        return populationScaler;
    }
}
