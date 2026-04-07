namespace Engine.Protocol;

/// <summary>
/// Initialization data sent by Engine on startup.
/// Contains configuration limits and available resources for the simulation.
/// </summary>
public sealed record InitData(
    List<WeightRange> WeightRanges,
    List<Charger> Chargers,
    List<Station> Stations);

public sealed record WeightRange(
    int Id,
    float Minimum,
    float Maximum);

public sealed record Charger(
    int Id,
    int MaxPowerKw,
    bool IsDual,
    int StationId);

public sealed record Station(
    uint Id,
    Position Position,
    string Address);
