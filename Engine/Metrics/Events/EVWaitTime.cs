namespace Engine.Metrics.Events;

using Core.Shared;

/// <summary>
/// Represents a metric captured when an EV stops waiting in a queue and begins a charging session.
/// </summary>
public readonly struct EVWaitTimeMetric
{
    /// <summary>Gets the unique identifier of the EV.</summary>
    required public int EVId { get; init; }

    /// <summary>Gets the identifier of the station where the charging occurs.</summary>
    required public int StationId { get; init; }

    /// <summary>Gets the time the EV arrived at the station and joined the queue.</summary>
    required public Time ArrivalAtStationTime { get; init; }

    /// <summary>Gets the time the EV actually connected to the charger and started drawing power.</summary>
    required public Time StartChargingTime { get; init; }

    /// <summary>Gets the total duration spent waiting in the queue.</summary>
    public Time WaitTime => ArrivalAtStationTime - StartChargingTime;
}