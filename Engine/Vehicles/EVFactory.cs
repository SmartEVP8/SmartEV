namespace Engine.Vehicles;

using Core.Vehicles;
using Core.Vehicles.Configurations;

/// <summary>
/// Factory for creating EVs, supporting for single or batch creation.
/// </summary>
/// <param name="config">The Configuration aggregate used to define EV properties.</param>
/// <param name="random">An instance of Random.</param>
public class EVFactory(EVConfig config, Random random)
{
    private uint _nextId = 1;

    /// <summary>
    /// Used to create a single EV.
    /// </summary>
    /// <returns>An EV conforming to the supplied configs.</returns>
    public EV Create()
    {
        var batteryConfig = SampleBatteryConfig();

        var capacity = batteryConfig.MinCapacityKWh
            + ((batteryConfig.MaxCapacityKWh - batteryConfig.MinCapacityKWh) * random.NextSingle());

        var chargeRate = batteryConfig.MinChargeRateKW
            + ((batteryConfig.MaxChargeRateKW - batteryConfig.MinChargeRateKW) * random.NextSingle());

        var battery = new Battery((ushort)capacity, (ushort)chargeRate, capacity, batteryConfig.Socket);

        var priceSensitivity = config.PrefsConfig.MinPriceSensitivity
            + ((config.PrefsConfig.MaxPriceSensitivity - config.PrefsConfig.MinPriceSensitivity) * random.NextSingle());

        var preferences = new Preferences(priceSensitivity);

        return new EV(_nextId++, battery, preferences);
    }

    /// <summary>
    /// Used for batch creation of EVs.
    /// </summary>
    /// <param name="amount">The amount of EVs to create.</param>
    /// <returns>A list of EVs.</returns>
    public List<EV> CreateFleet(int amount)
    {
        var fleet = new List<EV>(amount);
        for (var i = 0; i < amount; i++)
            fleet.Add(Create());
        return fleet;
    }

    private BatteryConfig SampleBatteryConfig()
    {
        var totalWeight = config.BatteryDistribution.Sum(e => e.Weight);
        var target = random.NextDouble() * totalWeight;
        var cumulative = 0.0;
        foreach (var entry in config.BatteryDistribution)
        {
            cumulative += entry.Weight;
            if (target <= cumulative)
                return entry.Config;
        }

        return config.BatteryDistribution[^1].Config;
    }
}
