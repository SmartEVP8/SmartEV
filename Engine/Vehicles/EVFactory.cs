namespace Engine.Vehicles;

using Core.Vehicles;
using Core.Vehicles.Configs;
using Engine.Spawning;

/// <summary>
/// Factory for creating EVs, supporting for single or batch creation.
/// </summary>
/// <param name="random">An instance of Random.</param>
public class EVFactory(Random random)
{
    private readonly EVConfig[] _models = EVModels.Models;
    private readonly Random _random = random;
    private readonly AliasSampler _sampler = new([.. EVModels.Models.Select(m => m.SpawnChance)]);

    /// <summary>
    /// Used to create a single EV.
    /// </summary>
    /// <returns>An EV conforming to the supplied configs.</returns>
    public EV Create()
    {
        var config = _models[_sampler.Sample(_random)];
        var batteryConfig = config.BatteryConfig;
        var maxCapacity = batteryConfig.MaxCapacityKWh;
        var chargeRate = batteryConfig.ChargeRateKW;
        var currCharge = maxCapacity * NextFloatInRange(0.2f, 1f);
        var priceSensPref = _random.NextSingle();

        var battery = new Battery(maxCapacity, chargeRate, currCharge, batteryConfig.Socket);
        var preferences = new Preferences(priceSensPref);

        return new EV(battery, preferences);
    }

    /// <summary>
    /// Scale the value to be between min and max.
    /// </summary>
    /// <param name="min">Minimum value to sample from.</param>
    /// <param name="max">Maximum value to sample from.</param>
    private float NextFloatInRange(float min, float max) => min + ((max - min) * _random.NextSingle());
}
