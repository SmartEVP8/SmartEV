namespace Engine.Events;

public class EventScheduler
{
    private readonly PriorityQueue<Event, (uint, uint)> _eventPriorityQueue = new();
    private readonly HashSet<(uint, ushort)> _canceledEvents = new();
    private uint _currentTime = 0;
    private uint _evSequeenceId = 0;

    public void ScheduleEvent(Event e)
    {
        var timestamp = e.Time;
        if (timestamp < _currentTime)
            throw new ArgumentOutOfRangeException($"Event timestamp {timestamp} is in the past (current time: {_currentTime})");
        _eventPriorityQueue.Enqueue(e, (timestamp, _evSequeenceId++));
    }

    /// <summary>
    /// Returns the next event in the priority queue, or null if there are no more events.
    /// If the event has been cancelled, it will be skipped and the next event will be returned instead.
    /// </summary>
    /// <returns>The next event in the queue to get resolved.</returns>
    public Event? GetNextEvent()
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

        return e;
    }

    public uint GetCurrentTime() => _currentTime;

    /// <summary>
    /// Cancels a reservation request by adding it to the set of canceled events.
    /// When the event is dequeued, it will be skipped.
    /// </summary>
    /// <param name="request">The CancelRequest for a given event.</param>
    public void CancelEvent(CancelRequest request)
    {
        var e = (request.EVId, request.StationId);
        _canceledEvents.Add(e);
    }
}
