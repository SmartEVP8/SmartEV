namespace Engine.Vehicles;

using Core.Vehicles;
using Core.Vehicles.Configs;

/// <summary>
/// Factory for creating EVs, supporting for single or batch creation.
/// </summary>
/// <param name="random">An instance of Random.</param>
public class EVFactory(Random random)
{
    private readonly EVConfig[] _models = EVModels.Models;
    private readonly Random _random = random;
    private uint _nextId = 1;

    /// <summary>
    /// Used to create a single EV.
    /// </summary>
    /// <returns>An EV conforming to the supplied configs.</returns>
    public EV Create()
    {
        var config = SampleConfigBySpawnChance();
        var batteryConfig = config.BatteryConfig;
        var maxCapacity = batteryConfig.MaxCapacityKWh;
        var chargeRate = batteryConfig.ChargeRateKW;
        var currCharge = maxCapacity * NextFloatInRange(0.2f, 1f);
        var priceSensPref = _random.NextSingle();

        var battery = new Battery(maxCapacity, chargeRate, currCharge, batteryConfig.Socket);

        var preferences = new Preferences(priceSensPref);

        return new EV(_nextId++, battery, preferences);
    }

    /// <summary>
    /// Used for batch creation of EVs.
    /// </summary>
    /// <param name="amount">The amount of EVs to create.</param>
    /// <returns>An array of EVs.</returns>
    public EV[] CreateFleet(int amount)
    {
        var fleet = new EV[amount];
        for (var i = 0; i < amount; i++)
            fleet[i] = Create();
        return fleet;
    }

    /// <summary>
    /// Scale the value to be between min and max.
    /// </summary>
    /// <param name="min">Minimum value to sample from.</param>
    /// <param name="max">Maximum value to sample from.</param>
    private float NextFloatInRange(float min, float max) => min + ((max - min) * _random.NextSingle());

    /// <summary>
    /// Samples a random (see cref="Core.Vehicles.Configs.EVConfig").
    /// </summary>
    private EVConfig SampleConfigBySpawnChance()
    {
        var target = _random.NextSingle() * 100;
        var cumulative = 0f;

        foreach (var model in _models)
        {
            cumulative += model.SpawnChance;
            if (target <= cumulative)
                return model;
        }

        return _models[^1];
    }
}
