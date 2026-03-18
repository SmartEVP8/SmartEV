namespace Core.Vehicles;

using Core.Shared;
using Core.Vehicles.Configs;

public class EV(uint id, Battery battery, Preferences preferences, EVConfig config)
{
    public readonly uint Id = id;
    public readonly Preferences Preferences = preferences;
    private Battery _battery = battery;
    private EVConfig _config = config;

    public EVConfig GetConfig() => _config;

    public Battery GetBattery() => _battery;
}
