namespace Engine.Events;

using Core.Shared;

/// <summary>
/// An EventScheduler responsbile for managing the scheduling and retrieval of events in the simulation. 
/// </summary>
public class EventScheduler
{
    private readonly PriorityQueue<Event, (uint, uint)> _eventPriorityQueue = new();
    private readonly HashSet<int> _canceledEvents = [];
    private Time _currentTime = 0;
    private Time _evSequeenceId = 0;

    /// <summary>
    /// Schedules an event to be executed at a specific timestamp.
    /// </summary>
    /// <param name="e">The event to schedule.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the timestamp is in the past.</exception>
    public void ScheduleEvent(Event e)
    {
        var timestamp = e.Time;
        if (timestamp < _currentTime)
            throw new ArgumentOutOfRangeException($"Event timestamp {timestamp} is in the past (current time: {_currentTime})");
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
        return e;
    }

    /// <summary>
    /// Gets the current simulation time in seconds.
    /// </summary>
    /// <returns>The current simulation time in seconds.</returns>
    public Time GetCurrentTime() => _currentTime;

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
}