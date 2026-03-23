namespace Engine.Events;

public class EventScheduler
{
    private readonly PriorityQueue<Event, (uint, uint)> _eventPriorityQueue = new();
    private readonly List<uint> _canceledEvents = new();
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
        if (e is CancelableEvent cancelableEvent && _canceledEvents.Contains(cancelableEvent.EVId))
        {
            _canceledEvents.Remove(cancelableEvent.EVId);
            return GetNextEvent();
        }

        return e;
    }

    public uint GetCurrentTime() => _currentTime;

    /// <summary>
    /// Cancels a CancelableEvent by adding it to the set of canceled events.
    /// When the event is dequeued, it will be skipped.
    /// </summary>
    /// <param name="evID">The evID from which a CancelableEvent should be cancelled bu.</param>
    public void CancelEvent(uint evID)
    {
        if (_canceledEvents.Contains(evID))
            throw new ArgumentException($"Event with EVId {evID} is already cancelled.");
        _canceledEvents.Add(evID);
    }
}
