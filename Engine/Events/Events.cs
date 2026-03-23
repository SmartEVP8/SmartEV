namespace Engine.Events;

using Core.Shared;

public abstract record Event(Time Time);

/// <summary>
/// MiddlewareEvents has handlers that are called when scheduled.
/// </summary>
public interface IMiddlewareEvent
{
}

public record FindCandidateStations(uint EVId, Time Time) : Event(Time), IMiddlewareEvent;
public record ReservationRequest(uint EVId, ushort StationId, Time Time) : Event(Time);
public record CancelRequest(uint EVId, ushort StationId, Time Time) : Event(Time);
public record ArriveAtStation(uint EVId, ushort StationId, Time Time) : Event(Time);
public record StartCharging(uint EVId, int ChargerId, Time Time) : Event(Time);
public record EndCharging(uint EVId, int ChargerId, Time Time) : Event(Time);
public record ArriveAtDestination(uint EVId, Time Time) : Event(Time);
