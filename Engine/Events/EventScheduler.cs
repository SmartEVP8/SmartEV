namespace Engine.Events;

public class EventScheduler(Dictionary<IMiddlewareEvent, Action<IMiddlewareEvent>> preProcessors)
{
    private readonly PriorityQueue<Event, (uint, uint)> _eventPriorityQueue = new();
    private readonly HashSet<(uint, ushort)> _canceledEvents = [];

    /// <summary>
    /// Optional event handlers that can perform actions on scheduleEvent.
    /// </summary>
    private readonly Dictionary<IMiddlewareEvent, Action<IMiddlewareEvent>> _preProcessors = preProcessors;
    private uint _currentTime = 0;
    private uint _evSequeenceId = 0;

    /// <summary>
    /// Schedules the event <paramref name="e"/> at its Time attribute.
    /// Potentially calls pre-processor if one is registered for that event type.
    /// </summary>
    /// <param name="e">The event to be scheduled.</param>
    /// <exception cref="ArgumentOutOfRangeException">If the events timestamp is before current time.</exception>
    public void ScheduleEvent(Event e)
    {
        var timestamp = e.Time;
        if (timestamp < _currentTime)
            throw new ArgumentOutOfRangeException($"Event timestamp {timestamp} is in the past (current time: {_currentTime})");

        if (e is IMiddlewareEvent me && _preProcessors.TryGetValue(me, out var handler))
            handler.Invoke(me);

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

    /// <summary>
    /// Gets the current timestamp of the event scheduler.
    /// </summary>
    /// <remark>
    /// The time is monotonically increasing.
    /// </remark
    public uint CurrentTime => _currentTime;

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
