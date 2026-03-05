namespace Core.Vehicles;

using Core.Shared;

// 4 + 4 + 9 = 17 bytes
public class EV(uint id, Battery battery, Preferences preferences)
{
    public readonly uint Id = id; // 4 bytes
    public readonly Preferences Preferences = preferences; // 4 bytes
    private Battery _battery = battery; // 9 bytes
    // Methods that update battery
}
