namespace Engine.Vehicles;

using Core.Routing;
using Core.Shared;
using Core.Vehicles;
using Engine.Routing;
using Engine.Spawning;
using Engine.Utils;

/// <summary>
/// Factory for creating EVs, supporting for single or batch creation.
/// </summary>
/// <param name="random">An instance of Random.</param>
public class EVFactory(Random random, IJourneySamplerProvider samplersProvider, IPointToPointRouter pointToPointRouter)
{
    private readonly AliasSampler _sampler = new([.. EVModels.Models.Select(m => m.SpawnChance)]);

    /// <summary>
    /// Used to create a single EV.
    /// </summary>
    /// <param name="departure">The depature of the created EV's journey.</param>
    /// <returns>An EV conforming to the supplied configs.</returns>
    public EV Create(Time departure)
    {
        var config = EVModels.Models[_sampler.Sample(random)];
        var batteryConfig = config.BatteryConfig;
        var maxCapacity = batteryConfig.MaxCapacityKWh;
        var chargeRate = batteryConfig.ChargeRateKW;
        var currCharge = maxCapacity * NextFloatInRange(0.2f, 1f);
        var priceSensPref = random.NextSingle();
        var minAcceptableCharge = NextFloatInRange(0.05f, 0.4f);

        var battery = new Battery(maxCapacity, chargeRate, currCharge, batteryConfig.Socket);
        var preferences = new Preferences(priceSensPref, minAcceptableCharge);
        var journey = CreateJourney(departure);
        return new EV(battery, preferences, journey, config.Efficiency);
    }

    private Journey CreateJourney(Time departure)
    {
        var (source, destination) = samplersProvider.Current.SampleSourceToDest(random);
        var (duration, polyline) = pointToPointRouter.QuerySingleDestination(
                        source.Longitude,
                        source.Latitude,
                        destination.Longitude,
                        destination.Latitude);

        var res = Polyline6ToPoints.DecodePolyline(polyline);
        return new Journey(departure, (Time)(uint)duration, res);
    }

    /// <summary>
    /// Scale the value to be between min and max.
    /// </summary>
    /// <param name="min">Minimum value to sample from.</param>
    /// <param name="max">Maximum value to sample from.</param>
    private float NextFloatInRange(float min, float max) => min + ((max - min) * random.NextSingle());
}
