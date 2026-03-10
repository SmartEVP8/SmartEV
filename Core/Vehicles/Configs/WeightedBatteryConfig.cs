namespace Core.Vehicles.Configurations;

/// <summary>
/// An aggregate of a battery config and a weight, 
/// used to add relative frequency in conjunction with other WeightedBatteryConfigs.
/// </summary>
/// <param name="config">A battery config.</param>
/// <param name="weight">An integer weight.</param>
public readonly struct WeightedBatteryConfig(BatteryConfig config, double weight = 1.0)
{
    /// <summary>Gets the configuration of the battery.</summary>
    public readonly BatteryConfig Config = config;

    /// <summary>Gets Relative weight for sampling. Higher values increase selection probability.</summary>
    public readonly double Weight = weight;
}
