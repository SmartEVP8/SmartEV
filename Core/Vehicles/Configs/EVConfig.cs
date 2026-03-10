namespace Core.Vehicles.Configs;

/// <summary>
/// Configuration for EVs. 
/// </summary>
/// <param name="batteryDistribution">A battery configuration with a weight.</param>
/// <param name="prefsConfig">A config for preferences.</param>
public class EVConfig(IReadOnlyList<WeightedBatteryConfig> batteryDistribution, PrefsConfig prefsConfig)
{
    /// <summary>Gets the battery configuration.</summary>
    public IReadOnlyList<WeightedBatteryConfig> BatteryDistribution { get; } = batteryDistribution;

    /// <summary>Gets the preference configuration.</summary>
    public PrefsConfig PrefsConfig { get; } = prefsConfig;
}
