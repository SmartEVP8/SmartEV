namespace Engine.Events;

public class EventScheduler
{
    private readonly PriorityQueue<IEvent, (uint, uint)> _eventPriorityQueue = new();
    private readonly HashSet<IEvent> _canceledEvents = new();
    private uint _currentTime = 0;
    private uint _evSequeenceId = 0;

    public void ScheduleEvent(IEvent e, uint timestamp)
    {
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
        if (!_canceledEvents.Contains(e))
        {
            return e;
        }

        _canceledEvents.Remove(e);
        return GetNextEvent();
    }

    public uint GetCurrentTime() => _currentTime;

    public void CancelEvent(CancelRequest request)
    {
        var (e, _) = _eventPriorityQueue.UnorderedItems.FirstOrDefault(e =>
            e.Element is ReservationRequest r && r.EVId == request.EVId && r.StationId == request.StationId);
        _canceledEvents.Add(e);
    }
}
