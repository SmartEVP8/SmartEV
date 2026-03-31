namespace Engine.Events;

using Core.Shared;
using Engine.Vehicles;

/// <summary>
/// Service handler that checks if EV's need to update their charge level.
/// </summary>
/// <param name="eventScheduler">Simulation event scheduler.</param>
/// <param name="evStore">EV store of entities.</param>
public class CheckAndUpdateAllEVsHandler(
    EventScheduler eventScheduler,
    EVStore evStore)
{
    /// <summary>
    /// Handles the CheckAndUpdateAllEVs event by scheduling a CheckUrgency event for each EV in the store.
    /// It also schedules the next CheckAndUpdateAllEVs event based on the specified interval size.
    /// </summary>
    /// <param name="e">The event for checking and updating all EVs.</param>
    public void Handle(CheckAndUpdateEV e)
    {
        var currentTime = eventScheduler.CurrentTime;

        // TODO: FIGURE OUT WHEN
        var nextTime = (Time)0;
        eventScheduler.ScheduleEvent(new CheckAndUpdateEV(e.EVId, nextTime));
    }
}
