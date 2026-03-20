namespace Core.Vehicles;

using Core.Routing;

public struct EV(Battery battery, Preferences preferences, Journey journey, ushort efficiency)
{
    public readonly Preferences Preferences = preferences;
    public Battery Battery { get; } = battery;
    public ushort Efficiency { get; } = efficiency;
    private Journey _journey = journey;
}
