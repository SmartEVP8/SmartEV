namespace Core.Vehicles;

using Core.Shared;

public class Battery(ushort capacity, ushort maxChargeRate, float currentCharge, Socket socket)
{
    public readonly ushort Capacity = capacity; // 2 bytes
    public readonly ushort MaxChargeRate = maxChargeRate; // 2 bytes

    public float CurrentCharge { get; private set; } = currentCharge; // 4 bytes

    public readonly Socket Socket = socket; // 1 byte

    /// <summary>
    /// Updates the current charge level of the battery, ensuring it stays within valid bounds (0 to Capacity).
    /// </summary>
    /// <param name="charge">The new charge level.</param>
    public void SetCharge(float charge) => CurrentCharge = Math.Clamp(charge, 0, Capacity);

}
