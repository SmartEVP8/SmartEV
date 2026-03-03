namespace Core.Vehicles;

using Core.Shared;

public class EV(uint id, Position position, Battery battery, Preferences preferences)
{
    public readonly uint Id = id;
    public readonly Preferences Preferences = preferences;
    private Position _position = position;
    private Battery _battery = battery;

    // Methods that update battery
}

