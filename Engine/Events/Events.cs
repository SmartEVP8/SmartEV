namespace Engine.Events;

public record struct ReservationRequest(uint EVId, ushort StationId, int Time) : IEvent
{
    public bool HasBeenCancelled { get; set; }
}

public record struct CancelRequest(uint EVId, ushort StationId, int Time) : IEvent
{
    public bool HasBeenCancelled { get; set; }
}

public record struct ArriveAtStation(uint EVId, ushort StationId, int Time) : IEvent
{
    public bool HasBeenCancelled { get; set; }
}

public record struct StartCharging(uint EVId, int ChargerId, int Time) : IEvent
{
    public bool HasBeenCancelled { get; set; }
}

public record struct EndCharging(uint EVId, int ChargerId, int Time) : IEvent
{
    public bool HasBeenCancelled { get; set; }
}

public record struct ArriveAtDestination(uint EVId, int Time) : IEvent
{
    public bool HasBeenCancelled { get; set; }
}
