namespace Engine.Events;

using Core.Shared;

public abstract record Event(uint EVId, Time Time);

public record ReservationRequest(uint EVId, ushort StationId, Time Time) : Event(EVId, Time);
public record CancelRequest(uint EVId, ushort StationId, Time Time) : Event(EVId, Time);
public record ArriveAtStation(uint EVId, ushort StationId, Time Time) : Event(EVId, Time);
public record StartCharging(uint EVId, int ChargerId, Time Time) : Event(EVId, Time);
public record EndCharging(uint EVId, int ChargerId, Time Time) : Event(EVId, Time);
public record ArriveAtDestination(uint EVId, Time Time) : Event(EVId, Time);
