namespace Core.Vehicles;

using Core.Routing;

/// <summary>
/// Represents an electric vehicle (EV) with a battery, preferences, a journey, and an efficiency rating. 
/// </summary>
/// <param name="battery">The battery of the EV.</param>
/// <param name="preferences">The preferences of the EV.</param>
/// <param name="journey">The journey of the EV.</param>
/// <param name="efficiency">The efficiency rating of the EV.</param>
public struct EV(Battery battery, Preferences preferences, Journey journey, ushort efficiency)
{
    /// <summary>
    /// Gets the preferences of the EV.
    /// </summary>
    public Preferences Preferences { get; } = preferences;

    /// <summary>
    /// Gets the battery of the EV.
    /// </summary>
    public Battery Battery { get; } = battery;

    /// <summary>
    /// Gets the efficiency rating of the EV.
    /// </summary>
    public ushort Efficiency { get; } = efficiency;

    /// <summary>
    /// Gets the journey of the EV.
    /// </summary>
    public Journey Journey { get; private set; } = journey;
  
    /// <summary>
    /// Check if the EV is charging
    /// </summary>
    public bool IsCharging { get; set; } = false;
}
