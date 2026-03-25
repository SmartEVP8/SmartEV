namespace Core.Vehicles;

/// <summary>
/// Represents the preferences of an EV driver.
/// </summary>
/// <param name="priceSensitivity">The price sensitivity of the driver.</param>
/// <param name="minAcceptableCharge">The minimum acceptable state of charge.</param>
/// <param name="maxPathDeviation">Maximum path deviation as a radius.</param>
public class Preferences(float priceSensitivity, float minAcceptableCharge, double maxPathDeviation)
{
    /// <summary>Gets the price sensitivity of the driver.</summary>
    public float PriceSensitivity { get; } = priceSensitivity;

    /// <summary>Gets the minimum acceptable state of charge.</summary>
    public float MinAcceptableCharge { get; } = minAcceptableCharge;

    /// <summary>Gets the maximum path deviation as a radius.</summary>
    public double MaxPathDeviation { get; } = maxPathDeviation;
}