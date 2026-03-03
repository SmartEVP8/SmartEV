namespace Core.Vehicles;

using Core.Shared;

public class Battery(float capacity, float maxChargeRate, float currentCharge, Socket socket)
{
    public readonly float Capacity = capacity;
    public readonly float MaxChargeRate = maxChargeRate;
    public float CurrentCharge { get; } = currentCharge;
    public readonly Socket Socket = socket;
}
