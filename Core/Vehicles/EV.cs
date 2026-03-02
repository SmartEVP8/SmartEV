namespace Core.Vehicles;

using Core.Shared;

public class EV(uint id, Position position, Battery battery, Preferences preferences)
{
    public readonly uint id = id;
    public readonly Preferences Preferences = preferences;
    private Position position = position;
    private Battery battery = battery;

    // Methods that update battery
}

