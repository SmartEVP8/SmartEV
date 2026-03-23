namespace Core.Vehicles;

public class Preferences(float priceSensitivity, float minAcceptableCharge, double maxPathDeviation)
{
    public readonly float PriceSensitivity = priceSensitivity;
    public readonly float MinAcceptableCharge = minAcceptableCharge;

    /// <summary>
    /// Maxiumum path deviation as a radius.
    /// </summary>
    public readonly double MaxPathDeviation = maxPathDeviation;
}
