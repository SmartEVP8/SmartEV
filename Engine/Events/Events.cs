namespace Engine.Events;

using Core.Shared;

public abstract record Event(Time Time);
public abstract record CancelableEvent(uint EVId, Time Time) : Event(Time);

public record ReservationRequest(uint EVId, ushort StationId, Time Time) : Event(Time);
public record CancelRequest(uint EVId, ushort StationId, Time Time) : Event(Time);
public record ArriveAtStation(uint EVId, ushort StationId, Time Time) : CancelableEvent(EVId, Time);
public record StartCharging(uint EVId, int ChargerId, Time Time) : Event(Time);
public record EndCharging(uint EVId, int ChargerId, Time Time) : CancelableEvent(EVId, Time);
public record ArriveAtDestination(uint EVId, Time Time) : CancelableEvent(EVId, Time);
