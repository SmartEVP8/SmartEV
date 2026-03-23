using Core.Vehicles;
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
        var evId = GetEventEVId(e);
        if (_canceledEvents.Contains((evId, eventType)) && evId != uint.MaxValue)
        {
            _canceledEvents.Remove((evId, eventType));
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
        var evId = GetEventEVId(request);
        if (evId == uint.MaxValue) return;
        var e = (evId, request.GetType().ToString());
        _canceledEvents.Add(e);
    }

    private static uint GetEventEVId(Event e) => e switch
    {
        ReservationRequest r => r.EVId,
        CancelRequest c => c.EVId,
        ArriveAtStation a => a.EVId,
        StartCharging s => s.EVId,
        EndCharging l => l.EVId,
        ArriveAtDestination d => d.EVId,
        _ => uint.MaxValue,
    };
}
