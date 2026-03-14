namespace Core.Vehicles;

// 4 + 4 + 9 = 17 bytes
public struct EV(Battery battery, Preferences preferences)
{
    public readonly Preferences Preferences = preferences; // 4 bytes
    private Battery _battery = battery; // 9 bytes

    // Methods that update battery
}
