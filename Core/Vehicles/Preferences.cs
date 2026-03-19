namespace Core.Vehicles;

public class Preferences(float priceSensitivity)
{
    public readonly float PriceSensitivity = priceSensitivity;
    public readonly float MinAcceptableCharge = minAcceptableCharge;
}
