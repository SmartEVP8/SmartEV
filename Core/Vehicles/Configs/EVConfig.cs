namespace Core.Vehicles.Configs;

/// <summary>
/// Configuration for an EV model, used to instantiate <see cref="Core.Vehicles.EV"/> instances.
/// </summary>
public readonly struct EVConfig(string model, float spawnChance, string category, BatteryConfig batteryConfig, ushort efficiency)
{
    /// <summary>
    /// The make and model of the car.
    /// </summary>
    public readonly string Model = model;

    /// <summary>
    /// The chance to spawn the specific car.
    /// </summary>
    /// <remarks>Based on the on-the-road share that the specific car represents in Denmark.</remarks>
    public readonly float SpawnChance = spawnChance;

    /// <summary>
    /// The car category the car is part of.
    /// </summary>
    public readonly string Category = category;

    /// <summary>
    /// The battery configuration from <see cref="Core.Vehicles.Configs.BatteryConfig"/> describing capacity, charge rate, and socket type.
    /// </summary>
    public readonly BatteryConfig BatteryConfig = batteryConfig;

    /// <summary>
    /// The energy consumption of this model in Wh/km.
    /// </summary>
    public readonly float Efficiency = efficiency;
}
