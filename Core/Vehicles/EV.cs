namespace Core.Vehicles;

using Core.Shared;
using Core.Vehicles.Configs;

public class EV(uint id, Battery battery, Preferences preferences, EVConfig config)
{
    public readonly uint Id = id;
    public readonly Preferences Preferences = preferences;

    public Battery Battery { get; } = battery;

    public EVConfig Config { get; } = config;

}
