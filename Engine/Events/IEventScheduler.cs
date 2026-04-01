namespace Engine.Events;

/// <summary>
/// Interface for scheduling events in the system.
/// </summary>
public interface IEventScheduler
{
    /// <summary>
    /// Schedules an event to be executed at its specified time.
    /// </summary>
    /// <param name="e">The event to be scheduled.</param>
    /// <returns>A unique identifier for the scheduled event.</returns>
    public uint ScheduleEvent(Event e);
}
