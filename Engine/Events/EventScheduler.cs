namespace Engine.Events;

using Core.Shared;

/// <summary>
/// The EventScheduler is responsible for managing and scheduling events in the system.
/// </summary>
/// <param name="preProcessors">Middleware/Preprocessors that are fired on MiddlewareEvents if attatched.</param>
public class EventScheduler(Dictionary<Type, Action<IMiddlewareEvent>> preProcessors)
{
    private readonly PriorityQueue<Event, (uint, uint)> _eventPriorityQueue = new();

    /// <summary>
    /// Optional event handlers that can perform actions on scheduleEvent.
    /// </summary>
    private readonly Dictionary<Type, Action<IMiddlewareEvent>> _preProcessors = preProcessors;
    private readonly HashSet<int> _canceledEvents = [];
    private readonly HashSet<int> _pausedUrgencyEvents = [];
    private Time _currentTime = 0;
    private Time _evSequeenceId = 0;

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

        if (e is IMiddlewareEvent me && _preProcessors.TryGetValue(me.GetType(), out var handler))
            handler.Invoke(me);

        _eventPriorityQueue.Enqueue(e, (timestamp, _evSequeenceId++));
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
        if (e is CancelableEvent cancelableEvent && _canceledEvents.Contains(cancelableEvent.EVId))
        {
            _canceledEvents.Remove(cancelableEvent.EVId);
            return GetNextEvent();
        }
        if (e is contineousEvent contineousEvent && _pausedUrgencyEvents.Contains(contineousEvent.EVId))
        {
            _pausedUrgencyEvents.Remove(contineousEvent.EVId);
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
    /// <param name="evID">The evID from which a CancelableEvent should be cancelled bu.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when attempting to cancel an event for an EV that already has a pending
    /// cancellation, violating the invariant that an EV can only have one cancelable event at a time.
    /// </exception>
    public void CancelEvent(int evID)
    {
        if (_canceledEvents.Contains(evID))
            throw new InvalidOperationException($"Event with EVId {evID} is already cancelled.");
        _canceledEvents.Add(evID);
    }

    /// <summary>
    /// Pauses a contineousEvent by adding it to the set of paused events.
    /// When the event is dequeued, it will be skipped.
    /// </summary> <param name="evID">The evID from which a contineousEvent should be paused.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when attempting to pause an event for an EV that already has a pending
    /// pause, violating the invariant that an EV can only have one contineous event at a time.
    /// </exception>
    public void PauseUrgencyEvent(int evID)
    {
        if (_pausedUrgencyEvents.Contains(evID))
            throw new InvalidOperationException($"Event with EVId {evID} is already paused.");
        _pausedUrgencyEvents.Add(evID);
    }
}
