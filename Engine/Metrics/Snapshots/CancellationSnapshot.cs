namespace Engine.Metrics.Snapshots;

using Core.Shared;

/// <summary>
/// A snapshot of the world at the time a cancellation request is made.
/// </summary>
public record CancellationSnapshot(
    int EVId,
    ushort StationId,
    Time Time,
    float StateOfCharge);