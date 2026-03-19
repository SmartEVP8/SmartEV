namespace Engine.Events;

public readonly record struct ReservationRequest(uint EVId, ushort StationId, uint Time) : IEvent;

public readonly record struct CancelRequest(uint EVId, ushort StationId, uint Time) : IEvent;

public readonly record struct ArriveAtStation(uint EVId, ushort StationId, uint Time) : IEvent;

public readonly record struct StartCharging(uint EVId, int ChargerId, uint Time) : IEvent;

public readonly record struct EndCharging(uint EVId, int ChargerId, uint Time) : IEvent;

public readonly record struct ArriveAtDestination(uint EVId, uint Time) : IEvent;
