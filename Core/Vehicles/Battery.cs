namespace Core.Vehicles;

using Core.Shared;

public class Battery(ushort capacity, ushort maxChargeRate, float stateOfCharge, Socket socket)
{
    public readonly ushort Capacity = capacity; // 2 bytes
    public readonly ushort MaxChargeRate = maxChargeRate; // 2 bytes
    public float StateOfCharge { get; set; } = stateOfCharge; // 4 bytes
    public readonly Socket Socket = socket; // 1 byte
}
