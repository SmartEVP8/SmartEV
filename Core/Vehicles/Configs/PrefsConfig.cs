namespace Core.Vehicles.Configurations;

/// <summary>
/// Configuration for the distribution of EV driver preferences used when generating a fleet.
/// </summary>
/// <param name="minPriceSensitivity">The minimum price sensitivity, where lower values mean the driver is less affected by energy prices.</param>
/// <param name="maxPriceSensitivity">The maximum price sensitivity, where higher values mean the driver strongly prefers cheaper charging options.</param>
public readonly struct PrefsConfig(float minPriceSensitivity, float maxPriceSensitivity)
{
    /// <summary>The minimum price sensitivity a generated driver may have.</summary>
    public readonly float MinPriceSensitivity = minPriceSensitivity;

    /// <summary>The maximum price sensitivity a generated driver may have.</summary>
    public readonly float MaxPriceSensitivity = maxPriceSensitivity;
}