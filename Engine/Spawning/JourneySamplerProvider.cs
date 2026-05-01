namespace Engine.Spawning;

using Engine.Grid;
using Core.Shared;
using Serilog;
using System.Text.Json;

/// <summary>
/// A shared store of the currently computed samplers.
/// </summary>
public sealed class JourneySamplerProvider : IJourneySamplerProvider
{
    private readonly JourneyPipeline _pipeline;
    private readonly List<List<Position>> _wetPolygons;
    private readonly float _distanceScalar;

    public IJourneySampler Current { get; private set; }

    public JourneySamplerProvider(
        JourneyPipeline pipeline,
        float populationScalar,
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
