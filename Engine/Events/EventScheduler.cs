namespace Engine.Events;

public class EventScheduler
{
    private readonly PriorityQueue<IEvent, (uint, uint)> _eventPriorityQueue = new();
    private readonly HashSet<(uint, ushort)> _canceledEvents = new();
    private readonly Dictionary<uint, uint> _canceledEndChargingEVIds = new();
    private uint _currentTime = 0;
    private uint _evSequeenceId = 0;

    public int QueueCount => _eventPriorityQueue.Count;

    public void ScheduleEvent(IEvent e, uint timestamp)
    {
        Console.WriteLine($"ScheduleEvent timestamp={timestamp} type={e.GetType().Name}");

        if (timestamp < _currentTime)
            throw new ArgumentOutOfRangeException($"Event timestamp {timestamp} is in the past (current time: {_currentTime})");
        _eventPriorityQueue.Enqueue(e, (timestamp, _evSequeenceId++));
    }

    public IEvent? GetNextEvent()
    {
        if (_eventPriorityQueue.Count == 0)
            return null;

        _eventPriorityQueue.TryDequeue(out var e, out var priority);
        _currentTime = priority.Item1;

        if (e is ReservationRequest request && _canceledEvents.Contains((request.EVId, request.StationId)))
        {
            _canceledEvents.Remove((request.EVId, request.StationId));
            return GetNextEvent();
        }

        if (e is EndCharging endCharging &&
            _canceledEndChargingEVIds.TryGetValue(endCharging.EVId, out var cancelUntil) &&
            endCharging.Time <= cancelUntil)  // <= not 
        {
            return GetNextEvent();
        }

        return e;
    }

    public uint GetCurrentTime() => _currentTime;

    public void CancelEvent(CancelRequest request)
    {
        var e = (request.EVId, request.StationId);
        _canceledEvents.Add(e);
    }

    public void CancelEndCharging(uint evId, uint upToTimestamp)
        => _canceledEndChargingEVIds[evId] = upToTimestamp;

    public IEvent? PeekNextEvent()
    {
        if (_eventPriorityQueue.Count == 0) return null;
        _eventPriorityQueue.TryPeek(out var e, out _);
        return e;
    }
}