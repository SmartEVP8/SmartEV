namespace Engine.Events;

using Engine.Vehicles;
using Core.Shared;

/// <summary>
/// Service handler that checks if EV's need to update their charge level.
/// </summary>
/// <param name="eventScheduler">Simulation event scheduler.</param>
/// <param name="evStore">EV store of entities.</param>
/// <param name="intervalSize">How often we check all EV's soc level in seconds.</param>
/// <param name="BatteryInterval">The SoC e.g 0.05 for 5% change in SoC. If an EV's SoC changes by this amount or more, we will check if it needs to charge.</param>
public class CheckAndUpdateEVHandler(
    EventScheduler eventScheduler,
    EVStore evStore,
    Time intervalSize,
    float BatteryInterval)
{
    /// <summary>
    /// Handles the CheckAndUpdateEV event by scheduling a CheckUrgency event for an EV.
    /// It also schedules the next CheckAndUpdateEVs event based on the specified interval size.
    /// </summary>
    /// <param name="e">The event for checking and updating an EV.</param>
    public void Handle(CheckAndUpdateEV e)
    {
        var currentTime = eventScheduler.CurrentTime;
        ref var ev = ref evStore.Get(e.EVId);

        if (ev.Journey is null || ev.IsCharging || !ev.HasDeparted(currentTime) || ev.HasArrived(currentTime) || ev.Journey.OnRouteToDestination)
            return;

        if (ev.CanCompleteJourney(ev.Preferences.MinAcceptableCharge))
        {
            eventScheduler.ScheduleEvent(new ArriveAtDestination(e.EVId, e.Time));
            ev.Journey.OnRouteToDestination = true;
            return;
        }

        var socBefore = ev.Battery.StateOfCharge;
        ev.ConsumeEnergy(currentTime, currentTime + intervalSize);
        var socAfter = ev.Battery.StateOfCharge;

        var prevBucket = (int)(socBefore / BatteryInterval);
        var currentBucket = (int)(socAfter / BatteryInterval);

        if (currentBucket != prevBucket)
            eventScheduler.ScheduleEvent(new CheckUrgency(e.EVId, e.Time));

        eventScheduler.ScheduleEvent(new CheckAndUpdateEV(e.EVId, e.Time + intervalSize));
    }
}
