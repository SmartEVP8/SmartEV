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
//  - Should increase Expected Queue Size by 1 (EQS + 1).
//  - Produce an arrival event where the expected arrival time is Actual_Arrival_Time * +- 20%.
//  - Calculate what the path deviation to the station will be from the original journey.
// Metrics:
//  - Count of reservation requests and their timestamps for when the reservation requests are made.
public record ReservationRequest(int EVId, ushort StationId, Time Time) : Event(Time);

// Functionality:
//  - Should decrease Expected Queue Size by 1 (EQS - 1).
//  - Calculate the EVs path deviation of the original journey from its current position.
//  - Get its own urgency and sample once from the urgency graph.
// Metrics:
//  - Count of cancellation requests and their timestamps for when the cancellation requests are made.
public record CancelRequest(int EVId, ushort StationId, Time Time) : Event(Time);

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

// Functionality:
// - Updates all EV's parallelly to their SoC level and check if should schedule a CheckUrgency event
public record CheckAndUpdateAllEVs(Time Time) : Event(Time);

// ---------- NON-DOMAIN EVENTS ---------- //

// Spawn new EVs into the future.
// EV's are allocated up front and polled once their depature has been reached.
public record SpawnEVS(Time Time) : Event(Time);


// Functionality:
// - Checks if an EV should look for Stations
public record CheckUrgency(int EVId, Time Time) : Event(Time);

// Spawn
// Functionality:
//  - Spawn an EV .
// - Sample once from urgency to see if it needs to find a charger immediately.
public record SnapshotEvent(Time Time) : Event(Time);

// Check urgency
// Functionality:
//  - Method that checks the urgency for an interval that is based on some car SoC, like every 10% or so.
