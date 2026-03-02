namespace Core.Vehicles;

using Core.Shared;

public class Battery(float capacity, float maxChargeRate, float currentCharge, Socket socket)
{
    public readonly float capacity = capacity;
    public readonly float maxChargeRate = maxChargeRate;
    public float CurrentCharge { get; } = currentCharge;
    public readonly Socket Socket = socket;
}
