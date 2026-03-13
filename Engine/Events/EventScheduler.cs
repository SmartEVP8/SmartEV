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

    public IEvent? GetNextEvent()
    {
        if (_eventPriorityQueue.Count == 0)
            return null;

        _eventPriorityQueue.TryDequeue(out var e, out var priority);
        _currentTime = priority.Item1;
        return e;
    }

    public uint GetCurrentTime() => _currentTime;
}

