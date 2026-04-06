namespace Engine.Protocol;

/// <summary>
/// Simulation snapshot DTO. Plain C# records, mirrors protocol.SimulationSnapshot.
/// </summary>
public sealed record SimulationSnapshot(
    uint TotalEvs,
    uint TotalCharging,
    List<StationState> StationStates,
    ulong SimulationTimeMs);

public sealed record StationState(
    uint StationId,
    List<ChargerState> ChargerStates,
    List<EVOnRoute> EvsOnRoute);

public sealed record ChargerState(
    bool IsActive,
    float Utilization,
    uint ChargerId,
    uint QueueSize,
    List<EVChargerState> EvsInQueue);

public sealed record EVChargerState(
    int EvId,
    float Soc,
    float TargetSoc);

public sealed record EVOnRoute(
    int EvId,
    List<Position> Waypoints);

public sealed record Position(
    double Lat,
    double Lon);
