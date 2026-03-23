namespace Engine.Events;

public class EventScheduler
{
    private readonly PriorityQueue<Event, (uint, uint)> _eventPriorityQueue = new();
    private readonly HashSet<(uint, string)> _canceledEvents = new();
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
        var eventType = e.GetType().ToString();
        if (_canceledEvents.Contains((e.EVId, eventType)))
        {
            _canceledEvents.Remove((e.EVId, eventType));
            return GetNextEvent();
        }

        return e;
    }

    public uint GetCurrentTime() => _currentTime;

    /// <summary>
    /// Cancels an event by adding it to the set of canceled events.
    /// When the event is dequeued, it will be skipped.
    /// </summary>
    /// <param name="request">The event that needs to be cancelled.</param>
    public void CancelEvent(Event request)
    {
        var e = (request.EVId, request.GetType().ToString());
        _canceledEvents.Add(e);
    }
}
