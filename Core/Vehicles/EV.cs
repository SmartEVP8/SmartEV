namespace Core.Vehicles;

using Core.Shared;

// 4 + 4 + 9 = 17 bytes
public class EV(uint id, Battery battery, Preferences preferences)
{
    public readonly uint Id = id; // 4 bytes
    public readonly Preferences Preferences = preferences; // 4 bytes
    private readonly Battery _battery = battery; // 9 bytes

    // exposing battery properties and methods through the EV class for easier access and encapsulation.
    public float CurrentCharge => _battery.CurrentCharge;
    public ushort MaxChargeRate => _battery.MaxChargeRate;
    public Socket Socket => _battery.Socket;
    public ushort Capacity => _battery.Capacity;

    /// <summary>
    /// Updates the current charge level of the battery.
    /// </summary>
    /// <param name="charge">The new charge level.</param>
    public void SetCharge(float charge) => _battery.SetCharge(charge);

    /// <summary>
    /// Returns a snapshot of the battery's current state, which is used to tidy up paramaters in
    /// the charging logic.
    /// </summary>
    /// <returns>A snapshot of the battery's current state.</returns>
    public GetBattery GetBattery() => new(_battery.MaxChargeRate, _battery.CurrentCharge, _battery.Capacity);
}