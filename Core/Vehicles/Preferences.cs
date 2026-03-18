namespace Core.Vehicles;

public class Preferences(float priceSensitivity, float minAcceptableCharge)
{
    public readonly float PriceSensitivity = priceSensitivity;
    public readonly float MinAcceptableCharge = minAcceptableCharge;
}
