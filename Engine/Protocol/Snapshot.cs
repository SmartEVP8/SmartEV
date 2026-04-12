namespace Engine.Protocol;

/// <summary>
/// Simulation snapshot DTO. Plain C# records, mirrors protocol.SimulationSnapshot.
/// </summary>
public sealed record SimulationSnapshot(
    uint TotalEvs,
    uint TotalCharging,
    ulong SimulationTimeMs);
