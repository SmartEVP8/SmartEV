namespace Engine.Events;

using Core.Shared;

/// <summary>
/// The EventScheduler is responsible for managing and scheduling events in the system.
/// </summary>
/// <param name="preProcessors">Middleware/Preprocessors that are fired on MiddlewareEvents if attatched.</param>
public class EventScheduler(Dictionary<Type, Action<IMiddlewareEvent>> preProcessors)
{
    private readonly PriorityQueue<Event, (Time, uint)> _eventPriorityQueue = new();

    /// <summary>
    /// Optional event handlers that can perform actions on scheduleEvent.
    /// </summary>
    private readonly Dictionary<Type, Action<IMiddlewareEvent>> _preProcessors = preProcessors;
    private readonly HashSet<uint> _canceledEvents = [];
    private Time _currentTime = 0;
    private uint _evSequeenceId = 0;

    /// <summary>
    /// Schedules the event <paramref name="e"/> at its Time attribute.
    /// Potentially calls pre-processor if one is registered for that event type.
    /// </summary>
    /// <param name="e">The event to be scheduled.</param>
    /// <exception cref="ArgumentOutOfRangeException">If the events timestamp is before current time.</exception>
    /// <returns>The id to give to <see cref="CancelEvent(uint)"/> in order to cancel.</returns>
    public uint ScheduleEvent(Event e)
    {
        var timestamp = e.Time;
        if (timestamp < _currentTime)
            throw new ArgumentOutOfRangeException($"Event timestamp {timestamp} is in the past (current time: {_currentTime})");

        if (e is IMiddlewareEvent me && _preProcessors.TryGetValue(me.GetType(), out var handler))
            handler.Invoke(me);

        var id = _evSequeenceId++;
        _eventPriorityQueue.Enqueue(e, (timestamp, id));
        return id;
    }

    /// <summary>
    /// Retrives and removes the next event from the EventScheduler.
    /// </summary>
    /// <returns>The next event in the queue to get resolved.</returns>
    public Event? GetNextEvent()
    {
        if (_eventPriorityQueue.Count == 0)
            return null;

        _eventPriorityQueue.TryDequeue(out var e, out var priority);
        _currentTime = priority.Item1;
        if (_canceledEvents.Contains(priority.Item2))
        {
            _canceledEvents.Remove(priority.Item2);
            return GetNextEvent();
        }

        return e;
    }

    /// <summary>Gets the current timestamp of the event scheduler.</summary>
    /// <remark>The time is monotonically increasing.</remark
    public Time CurrentTime => _currentTime;

    /// <summary>
    /// Cancels a CancelableEvent by adding it to the set of canceled events.
    /// When the event is dequeued, it will be skipped.
    /// </summary>
    /// <param name="cancelId">The id from which a event should be cancelled.</param>
    public void CancelEvent(uint cancelId)
    {
        if (!_canceledEvents.Contains(cancelId))
            _canceledEvents.Add(cancelId);
    }
}
