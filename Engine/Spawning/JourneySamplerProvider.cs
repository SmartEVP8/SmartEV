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
    private readonly JourneySamplers[] _hourlySamplers = new JourneySamplers[24];

    private uint _currentHour = 0;

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

        Parallel.For(0, 24, hour => _hourlySamplers[hour] = EnsureSamplerOnDisk((uint)hour));

        Current = _hourlySamplers[0];
    }

    /// <inheritdoc/>
    public IJourneySampler Current { get; private set; }

    /// <inheritdoc/>
    public void SetCurrent(Time time)
    {
        if (time.Hours != _currentHour)
        {
            _currentHour = time.Hours;
            Current = _hourlySamplers[_currentHour];
        }
    }

    private JourneySamplers EnsureSamplerOnDisk(uint hour)
    {
        if (JourneySamplerCache.Exists(hour, _distanceScalar)) return LoadHourFromDisk(hour);

        var popScalar = GetScalers(hour);
        var journeyDTO = _pipeline.ComputeDTO(popScalar, _distanceScalar, _wetPolygons);
        var sampler = JourneySamplerCache.Write(hour, journeyDTO, _distanceScalar);
        return JourneyPipeline.FromDTO(sampler);
    }

    private JourneySamplers LoadHourFromDisk(uint hour)
    {
        var journeyDTO = JourneySamplerCache.Read(hour, _distanceScalar);
        return JourneyPipeline.FromDTO(journeyDTO);
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
