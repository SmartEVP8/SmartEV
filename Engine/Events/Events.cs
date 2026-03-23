namespace Engine.Events;

using Core.Shared;

public abstract record Event(Time Time);
public abstract record CancelableEvent(int EVId, Time Time) : Event(Time);

/// <summary>
/// MiddlewareEvents has handlers that are called when scheduled.
/// </summary>
public interface IMiddlewareEvent
{
}

public record FindCandidateStations(int EVId, Time Time) : Event(Time), IMiddlewareEvent;
public record ReservationRequest(int EVId, ushort StationId, Time Time) : Event(Time);
public record CancelRequest(int EVId, ushort StationId, Time Time) : Event(Time);
public record ArriveAtStation(int EVId, ushort StationId, Time Time) : CancelableEvent(EVId, Time);
public record StartCharging(int EVId, int ChargerId, Time Time) : Event(Time);
public record EndCharging(int EVId, int ChargerId, Time Time) : CancelableEvent(EVId, Time);
public record ArriveAtDestination(int EVId, Time Time) : CancelableEvent(EVId, Time);
