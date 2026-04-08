namespace Engine.Vehicles;

using Core.Routing;
using Core.Shared;
using Core.Vehicles;
using Core.Vehicles.Configs;
using Engine.Routing;
using Engine.Spawning;
using Engine.Utils;

/// <summary>
/// Factory for creating EVs, supporting single or batch creation.
/// </summary>
/// <param name="random">An instance of Random.</param>
/// <param name="samplersProvider">The provider of the samplers used to sample the EVs' journeys.</param>
/// <param name="pointToPointRouter">Used to get the duration and path of the EVs' journeys.</param>
public class EVFactory(Random random, IJourneySamplerProvider samplersProvider, IPointToPointRouter pointToPointRouter)
{
    private readonly AliasSampler _sampler = new([.. EVModels.Models.Select(m => m.SpawnChance)]);

    /// <summary>
    /// Creates a single EV. For batch creation use <see cref="SampleParams"/> and <see cref="Create(SampledEVParams, Time)"/>.
    /// </summary>
    /// <param name="departure">The departure time of the created EV's journey.</param>
    /// <returns>An EV conforming to the supplied configs.</returns>
    public EV Create(Time departure)
    {
        var p = SampleParams(1)[0];
        return Create(p, departure);
    }

    /// <summary>
    /// Contains all randomly sampled values needed to create an EV, allowing
    /// <see cref="Create(SampledEVParams, Time)"/> to be called without any further random sampling.
    /// </summary>
    public record SampledEVParams(
        EVConfig Config,
        float CurrCharge,
        float PriceSensPref,
        float MinAcceptableCharge,
        float MaxPathDeviation,
        (Position Source, Position Destination) SourceDest
    );

    /// <summary>
    /// Creates a single EV from pre-sampled parameters. This method is thread-safe
    /// and can be called in parallel as it performs no random sampling.
    /// </summary>
    /// <param name="p">The pre-sampled parameters produced by <see cref="SampleParams"/>.</param>
    /// <param name="departure">The departure time of the created EV's journey.</param>
    /// <returns>An EV conforming to the sampled parameters.</returns>
    public EV Create(SampledEVParams p, Time departure)
    {
        var batteryConfig = p.Config.BatteryConfig;
        var battery = new Battery(batteryConfig.MaxCapacityKWh, batteryConfig.ChargeRateKW, p.CurrCharge, batteryConfig.Socket);
        var preferences = new Preferences(p.PriceSensPref, p.MinAcceptableCharge, p.MaxPathDeviation);
        var journey = CreateJourney(departure, p.SourceDest);
        return new EV(battery, preferences, journey, p.Config.Efficiency);
    }

    /// <summary>
    /// Sequentially samples all random values needed to create <paramref name="amount"/> EVs.
    /// Must be called before <see cref="Create(SampledEVParams, Time)"/> when creating EVs in parallel.
    /// </summary>
    /// <param name="amount">The number of EVs to sample parameters for.</param>
    /// <returns>An array of <see cref="SampledEVParams"/> ready to be passed to <see cref="Create(SampledEVParams, Time)"/> .</returns>
    public SampledEVParams[] SampleParams(int amount)
    {
        var parameters = new SampledEVParams[amount];
        for (var i = 0; i < amount; i++)
        {
            parameters[i] = new SampledEVParams(
                Config: EVModels.Models[_sampler.Sample(random)],
                CurrCharge: NextFloatInRange(0.4f, 1f),
                PriceSensPref: random.NextSingle(),
                MinAcceptableCharge: NextFloatInRange(0.05f, 0.2f),
                MaxPathDeviation: NextFloatInRange(5.0f, 30.0f),
                SourceDest: samplersProvider.Current.SampleSourceToDest(random));
        }

        return parameters;
    }

    private Journey CreateJourney(Time departure, (Position Source, Position Destination) sourceDest)
    {
        var (source, destination) = sourceDest;
        var queryResult = pointToPointRouter.QuerySingleDestination(
            source.Longitude,
            source.Latitude,
            destination.Longitude,
            destination.Latitude);

        var segments = Polyline6ToPoints.DecodePolyline(queryResult.Polyline);
        var durationMs = (uint)Math.Ceiling(queryResult.Duration * Time.MillisecondsPerSecond);
        return new Journey(departure, (Time)durationMs, queryResult.Distance, segments);
    }

    /// <summary>
    /// Scales a random value to be between <paramref name="min"/> and <paramref name="max"/>.
    /// </summary>
    /// <param name="min">Minimum value to sample from.</param>
    /// <param name="max">Maximum value to sample from.</param>
    private float NextFloatInRange(float min, float max) => min + ((max - min) * random.NextSingle());
}
