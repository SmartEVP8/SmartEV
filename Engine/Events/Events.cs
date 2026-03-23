namespace Engine.Events;

using Core.Shared;

/// <remarks>
/// When adding new event types, make sure to update the GetEventEVId method have
/// the up to-date list of events that uses EVId.
/// </remarks>
public abstract record Event(Time Time);

public record ReservationRequest(uint EVId, ushort StationId, Time Time) : Event(Time);
public record CancelRequest(uint EVId, ushort StationId, Time Time) : Event(Time);
public record ArriveAtStation(uint EVId, ushort StationId, Time Time) : Event(Time);
public record StartCharging(uint EVId, int ChargerId, Time Time) : Event(Time);
public record EndCharging(uint EVId, int ChargerId, Time Time) : Event(Time);
public record ArriveAtDestination(uint EVId, Time Time) : Event(Time);
