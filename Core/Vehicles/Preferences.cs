namespace Core.Vehicles;

/// <summary>
/// Represents the preferences of an EV driver.
/// </summary>
/// <param name="priceSensitivity">The price sensitivity of the driver.</param>
/// <param name="minAcceptableCharge">The minimum acceptable state of charge.</param>
public class Preferences(float priceSensitivity, float minAcceptableCharge)
{
    /// <summary>Gets the price sensitivity of the driver.</summary>
    public float PriceSensitivity { get; } = priceSensitivity;

    /// <summary>Gets the minimum acceptable state of charge.</summary>
    public float MinAcceptableCharge { get; } = minAcceptableCharge;
}