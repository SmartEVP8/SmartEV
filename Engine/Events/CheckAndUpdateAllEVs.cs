namespace Engine.Events;

using Engine.Vehicles;
using Core.Shared;

/// <summary>
/// Service handler that checks if EV's need to update their charge level.
/// </summary>
/// <param name="eventScheduler">Simulation event scheduler.</param>
/// <param name="evStore">EV store of entities.</param>
/// <param name="intervalSize">How often we check all EV's soc level in seconds.</param>
/// <param name="BatteryInterval">The SoC% change.</param>
public class CheckAndUpdateAllEVsHandler(
    EventScheduler eventScheduler,
    EVStore evStore,
    Time intervalSize,
    ushort BatteryInterval)
{
    /// <summary>
    /// Handles the CheckAndUpdateAllEVs event by scheduling a CheckUrgency event for each EV in the store.
    /// It also schedules the next CheckAndUpdateAllEVs event based on the specified interval size.
    /// </summary>
    /// <param name="e">The event for checking and updating all EVs.</param>
    public void Handle(CheckAndUpdateAllEVs e)
    {
        var evsThatNeedChecking = new int[evStore.Count];
        Array.Fill(evsThatNeedChecking, -1);

        var currentTime = eventScheduler.CurrentTime;

        for (var evID = 0; evID < evStore.Count; evID++)
        {
            ref var ev = ref evStore.Get(evID);

            if (ev.Journey is null || ev.IsCharging || !ev.HasDeparted(currentTime) || ev.HasArrived(currentTime))
                continue;


            Console.WriteLine($"Time: {e.Time}, EvID: {evID}, Stats: {ev}");

            var socBefore = ev.Battery.StateOfCharge;
            ev.ConsumeEnergy(currentTime, currentTime + intervalSize);
            var socAfter = ev.Battery.StateOfCharge;

            var prevBucket = (int)(socBefore / BatteryInterval);
            var currentBucket = (int)(socAfter / BatteryInterval);

            if (currentBucket != prevBucket)
                evsThatNeedChecking[evID] = evID;
        }

        foreach (var evID in evsThatNeedChecking)
        {
            if (evID == -1) continue;
            eventScheduler.ScheduleEvent(new CheckUrgency(evID, e.Time));
        }

        eventScheduler.ScheduleEvent(new CheckAndUpdateAllEVs(e.Time + intervalSize));
    }
}
