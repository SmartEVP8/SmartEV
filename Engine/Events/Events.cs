namespace Engine.Events;

using Core.Shared;

public abstract record Event(Time Time);

/// <summary>
/// MiddlewareEvents has handlers that are called when scheduled.
/// </summary>
public interface IMiddlewareEvent
{
}

// Functionality:
//  - Should compute the distance from the EV's current position to all station candidates and back to their destination.
public record FindCandidateStations(int EVId, Time Time) : Event(Time), IMiddlewareEvent;

// Functionality:
//  - Should increase Actual Queue Size by 1 (AQS + 1).
//  - Method for either placing the EV in the queue or if there are no EVs in the queue, immediately start charging.
//  - Method that stores the EVs arrival time at the station.
public record ArriveAtStation(int EVId, ushort StationId, double TargetSoC, Time Time) : Event(Time);

// Functionality:
//  - Pop next EV from the the queue and start charging that EV and stores the EV's start charging time.
//  - Integrate the Recompute functionality.
// Metrics:
//  - Record the time an EV spent charging
public record EndCharging(int EVId, int ChargerId, Time Time) : Event(Time);

// Metrics:
//  - Record if the EV missed its deadline.
//  - Record how much the EV missed the deadline by.
//  - Record the path deviation of an EVs actual journey compared to its original journey.
public record ArriveAtDestination(int EVId, Time Time) : Event(Time);

// ---------- NON-DOMAIN EVENTS ---------- //

// Spawn new EVs into the future.
// EV's are allocated up front and polled once their depature has been reached.
public record SpawnEVS(Time Time) : Event(Time);

// Snapshot event for collecting metrics at regular intervals.
public record SnapshotEvent(Time Time) : Event(Time);
