namespace Engine.Events;

using Core.Shared;

/// <summary>
/// Interface for scheduling events in the simulation.
/// </summary>
public interface IEventScheduler
{
    /// <summary>
    /// Gets the current simulation time.
    /// </summary>
    Time CurrentTime { get; }

    /// <summary>
    /// Schedules an event to be executed at its specified time.
    /// </summary>
    /// <param name="e">The event to schedule.</param>
    /// <returns>The token for the scheduled event.</returns>
    uint ScheduleEvent(Event e);

    /// <summary>
    /// Gets the next event to be executed, or null if there are no more events.
    /// </summary>
    /// <returns>The next event to be executed, or null if there are no more events.</returns>
    /// 
    /// 
    /// 
    Event? GetNextEvent();
}
