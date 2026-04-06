namespace Engine.Protocol;

/// <summary>
/// Discriminated union for protocol events emitted from the Engine.
/// </summary>
public abstract record ProtocolEvent;

public sealed record ArrivalEvent(
    uint StationId,
    int EvId,
    ulong TimestampMs) : ProtocolEvent;

public sealed record ChargingEndEvent(
    uint StationId,
    int EvId,
    ulong TimestampMs) : ProtocolEvent;
