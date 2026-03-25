namespace Core.Vehicles;

using Core.Routing;

public struct EV(Battery battery, Preferences preferences, Journey journey, ushort efficiency)
{
    public readonly Preferences Preferences = preferences; // 8 bytes
    public Battery Battery { get; } = battery; // 8 bytes
    public Journey Journey { get; private set; } = journey; // 8 bytes
    public ushort Efficiency { get; } = efficiency; // 2
    public bool IsCharging { get; set; } = false; // 1 byte
}
