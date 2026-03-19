namespace Core.Vehicles;

using Core.Routing;

public struct EV(Battery battery, Preferences preferences, Journey journey)
{
    public readonly Preferences Preferences = preferences; // 4 bytes
    private Battery _battery = battery; // 9 bytes
    private Journey _journey = journey;
}
