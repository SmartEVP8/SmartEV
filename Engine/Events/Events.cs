namespace Engine.Events;

using Core.Charging;
using Engine.Metrics;

// ----------- DOMAIN EVENTS ----------- //

// Functionality: 
//  - Should increase Expected Queue Size by 1 (EQS + 1).
//  - Produce an arrival event where the expected arrival time is Actual_Arrival_Time * +- 20%.
//  - Calculate what the path deviation to the station will be from the original journey.
// Metrics:
//  - Count of reservation requests and their timestamps for when the reservation requests are made.
public readonly record struct ReservationRequest(uint EVId, ushort StationId, int Time) : IEvent;

// Functionality: 
//  - Should decrease Expected Queue Size by 1 (EQS - 1).
//  - Calculate the EVs path deviation of the original journey from its current position.
//  - Get its own urgency and sample once from the urgency graph.
// Metrics:
//  - Count of cancellation requests and their timestamps for when the cancellation requests are made.
public readonly record struct CancelRequest(uint EVId, ushort StationId, int Time) : IEvent;

// Functionality: 
//  - Should increase Actual Queue Size by 1 (AQS + 1).
//  - Method for either placing the EV in the queue or if there are no EVs in the queue, immediately start charging.
//  - Method that stores the EVs arrival time at the station.
public readonly record struct ArriveAtStation(uint EVId, ushort StationId, int Time) : IEvent;

// Functionality: 
//  - Pop next EV from the the queue and start charging that EV and stores the EV's start charging time.
//  - Integrate the Recompute functionality.
// Metrics:
//  - Record the time an EV spent charging
public readonly record struct EndCharging(uint EVId, int ChargerId, int Time) : IEvent;

// Functionality: 
//  - Pop next EV from the the queue and start charging that EV and stores the EV's start charging time.
//  - Integrate the Recompute functionality.
// Metrics:
//  - Record if the EV missed its deadline.
//  - Record how much the EV missed the deadline by.
//  - Record the path deviation of an EVs actual journey compared to its original journey.
public readonly record struct ArriveAtDestination(uint EVId, int Time) : IEvent;


// ---------- NON-DOMAIN EVENTS ---------- //

// Spawn
// Functionality:
//  - Spawn an EV .
//  - Sample once from urgency to see if it needs to find a charger immediately.

public readonly record struct SnapshotEvent(
    IReadOnlyList<Station> Stations,
    MetricsService Metrics,
    EventScheduler Scheduler,
    Func<ChargerBase, double> GetDeliveredKW
) : IEvent;

// Check urgency
// Functionality:
//  - Method that checks the urgency for an interval that is based on some car SoC, like every 10% or so.