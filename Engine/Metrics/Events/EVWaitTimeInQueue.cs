namespace Engine.Metrics.Events;

/// <summary>
/// Represents a metric captured when an EV stops waiting in a queue and begins a charging session.
/// </summary>
public record WaitTimeInQueueMetric
{
    /// <summary> Gets the ID of the EV that waited and started charging. </summary>
    required public int EVId { get; init; }

    /// <summary> Gets the ID of the station where the EV waited and started charging. </summary>
    required public ushort StationId { get; init; }

    /// <summary> Gets the simulation time when the EV arrived at the station. </summary>
    required public uint ArrivalAtStationTime { get; init; }

    /// <summary> Gets the simulation time when the EV started charging. </summary>
    required public uint StartChargingTime { get; init; }

    /// <summary> Gets the total time the EV spent waiting in the queue before starting to charge. </summary>
    public uint WaitTimeInQueue => StartChargingTime - ArrivalAtStationTime;
}
