namespace Engine.Events;

public class EventScheduler
{
    private readonly PriorityQueue<IEvent, (uint, uint)> _eventPriorityQueue = new();
    private uint _currentTime = 0;
    private uint _evSequeenceId = 0;

    public void ScheduleEvent(IEvent e, uint timestamp)
    {
        if (timestamp < _currentTime)
            throw new ArgumentOutOfRangeException($"Event timestamp {timestamp} is in the past (current time: {_currentTime})");
        _eventPriorityQueue.Enqueue(e, (timestamp, _evSequeenceId++));
    }

    /// <summary>
    /// Returns the next event in the priority queue, and updates the current time to the timestamp of that event.
    /// If the event is a reservation request that has been canceled, it will be skipped and the next event will be returned instead.
    /// If there are no more events in the queue, null will be returned.
    /// </summary>
    /// <returns>The next non-cancelled event in the queue.</returns>
    public IEvent? GetNextEvent()
    {
        if (_eventPriorityQueue.Count == 0)
            return null;

        _eventPriorityQueue.TryDequeue(out var e, out var priority);
        _currentTime = priority.Item1;
        if (e.HasBeenCancelled)
        {
            return GetNextEvent();
        }

        return e;
    }

    public uint GetCurrentTime() => _currentTime;

    public void CancelEvent(CancelRequest request)
    {
        _eventPriorityQueue.UnorderedItems.FirstOrDefault(e =>
            e.Element is ReservationRequest r &&
            r.EVId == request.EVId &&
            r.StationId == request.StationId).Element.HasBeenCancelled = true;
    }

}
