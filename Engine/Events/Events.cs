namespace Engine.Events;

public readonly record struct ReservationRequest(int EVId, ushort StationId, uint Time) : IEvent;

public readonly record struct CancelRequest(int EVId, ushort StationId, uint Time) : IEvent;

public readonly record struct ArriveAtStation(int EVId, ushort StationId, uint Time) : IEvent;

public readonly record struct StartCharging(int EVId, int ChargerId, uint Time) : IEvent;

public readonly record struct EndCharging(int EVId, int ChargerId, uint Time) : IEvent;

public readonly record struct ArriveAtDestination(int EVId, uint Time) : IEvent;
