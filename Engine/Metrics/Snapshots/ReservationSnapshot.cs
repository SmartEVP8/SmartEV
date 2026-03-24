namespace Engine.Metrics.Snapshots;

using Core.Shared;

/// <summary>
/// A snapshot of the world at the time a reservation request is made.
/// </summary>
public record ReservationSnapshot(
    int EVId,
    ushort StationId,
    Time Time,
    float Deviation,
    float StateOfCharge);