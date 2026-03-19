namespace Engine.Events;

/// <summary>
/// An EventScheduler responsbile for managing the scheduling and retrieval of events in the simulation. 
/// </summary>
public class EventScheduler
{
    private readonly PriorityQueue<IEvent, (uint, int)> _eventPriorityQueue = new();
    private readonly HashSet<(int, ushort)> _canceledEvents = new();
    private readonly Dictionary<int, uint> _canceledEndChargingEVIds = new();
    private uint _currentTime = 0;
    private int _evSequeenceId = 0;

    /// <summary>
    /// Gets the number of events current scheduled in the Eventsheduler.
    /// </summary>
    public int QueueCount => _eventPriorityQueue.Count;

    /// <summary>
    /// Schedules an event to be executed at a specific timestamp.
    /// </summary>
    /// <param name="e">The event to schedule.</param>
    /// <param name="timestamp">The timestamp at which to execute the event.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the timestamp is in the past.</exception>
    public void ScheduleEvent(IEvent e, uint timestamp)
    {
        if (timestamp < _currentTime)
            throw new ArgumentOutOfRangeException($"Event timestamp {timestamp} is in the past (current time: {_currentTime})");
        _eventPriorityQueue.Enqueue(e, (timestamp, _evSequeenceId++));
    }

    /// <summary>
    /// Retrives and removes the next event from the EventScheduler.
    /// </summary>
    /// <returns>The next event to be executed, or null if no events are scheduled.</returns>
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
            endCharging.Time <= cancelUntil)
        {
            _canceledEndChargingEVIds.Remove(endCharging.EVId);
            return GetNextEvent();
        }

        return e;
    }

    /// <summary>
    /// Gets the current simulation time in seconds.
    /// </summary>
    /// <returns>The current simulation time in seconds.</returns>
    public uint GetCurrentTime() => _currentTime;

    /// <summary>
    /// Cancels a previously scheduled event based on the provided CancelRequest.
    /// </summary>
    /// <param name="request">The cancel request containing the event details to cancel.</param>
    public void CancelEvent(CancelRequest request)
    {
        var e = (request.EVId, request.StationId);
        _canceledEvents.Add(e);
    }

    /// <summary>
    /// Cancels a scheduled EndCharging event for a specific EVId up to a certain timestamp.
    /// </summary>
    /// <param name="evId">The ID of the EV for which to cancel the charging event.</param>
    /// <param name="upToTimestamp">The timestamp up to which to cancel the event.</param>
    public void CancelEndCharging(int evId, uint upToTimestamp)
        => _canceledEndChargingEVIds[evId] = upToTimestamp;

    /// <summary>
    /// Peeks at the next event in the EventSheduler without removing it from the queue.
    /// </summary>
    /// <returns>The next event in the queue, or null if the queue is empty.</returns>
    public IEvent? PeekNextEvent()
    {
        if (_eventPriorityQueue.Count == 0) return null;
        _eventPriorityQueue.TryPeek(out var e, out _);
        return e;
    }
}