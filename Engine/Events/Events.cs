namespace Engine.Events;

// 1+QueueSize_E, Produce Arrival Event (Actual arrival time * +- 20%), Path Deviation
// Metrics: Count of reservation requests and timestamps
public readonly record struct ReservationRequest(uint EVId, ushort StationId, int Time) : IEvent;

// -1 QueueSize_E, Path Deviation, Sample once from urgency)
// Metrics: Count of cancellation requests and timestamps
public readonly record struct CancelRequest(uint EVId, ushort StationId, int Time) : IEvent;

// 1+QueueSize_A, Place EV in queue or start charging immediately, Store arrival time
public readonly record struct ArriveAtStation(uint EVId, ushort StationId, int Time) : IEvent;

//public readonly record struct StartCharging(uint EVId, int ChargerId, int Time) : IEvent;

// Pop next EV from queue and start charging, store start time, Recompute Integration
// Metrics: Time spent charging
public readonly record struct EndCharging(uint EVId, int ChargerId, int Time) : IEvent;

// Metrics: Missed deadlines/Arrival vs Expected arrival time, Path Deviation
public readonly record struct ArriveAtDestination(uint EVId, int Time) : IEvent;


//Non-domain events

//Spawn (Spawn EV and Sample once from urgency)

//Snapshot (iterate over stations and ...)
//Metrics: Utilization, queue size, price, number/% of active chargers

//Check urgency (Intercal based on car SoC)
