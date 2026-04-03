namespace Engine.Events;

using Core.Shared;

public interface IEventScheduler
{
    Time CurrentTime { get; }
    uint ScheduleEvent(Event e);
}

