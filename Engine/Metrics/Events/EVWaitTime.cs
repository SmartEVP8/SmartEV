namespace Engine.Metrics.Events;

using Core.Shared;

/// <summary>
/// Represents a metric captured when an EV stops waiting in a queue and begins a charging session.
/// </summary>
public record WaitTimeInQueueMetric // Changed to record for better data semantics
{
    /// <summary> Gets the ID of the EV that waited and started charging. </summary>
    required public int EVId { get; init; }

    /// <summary> Gets the ID of the station where the EV waited and started charging. </summary>
    required public ushort StationId { get; init; }

    /// <summary> Gets the simulation time when the EV arrived at the station. </summary>
    required public Time ArrivalAtStationTime { get; init; }

    /// <summary> Gets the simulation time when the EV started charging. </summary>
    required public Time StartChargingTime { get; init; }

    /// <summary> Gets the total time the EV spent waiting in the queue before starting to charge. </summary>
    public Time WaitTime => StartChargingTime - ArrivalAtStationTime;
}
