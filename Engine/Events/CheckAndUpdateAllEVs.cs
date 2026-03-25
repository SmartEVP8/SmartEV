namespace Engine.Events;

using Engine.Vehicles;
using Engine.GeoMath;
using Core.Shared;
using Core.Vehicles;

/// <summary>
/// Service handler that checks if EV's need to update their charge level.
/// </summary>
/// <param name="eventScheduler">Simulation event scheduler.</param>
/// <param name="evStore">EV store of entities.</param>
/// <param name="intervalSize">How often we check all EV's soc level.</param>
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
    /// <param name="checkAndUpdateAllEVs">The event for checking and updating all EVs.</param>
    public void Handle(CheckAndUpdateAllEVs checkAndUpdateAllEVs)
    {
        var evsThatNeedChecking = new int[evStore.Count];

        Parallel.For(0, evStore.Count, evID =>
        {
            ref var ev = ref evStore.Get(evID);

            if (ev.Journey is null || ev.IsCharging) return;

            var batteryLost = CalculateStateOfCharge(ev, intervalSize);
            ev.Battery.StateOfCharge -= batteryLost;

            var currentBucket = (int)(ev.Battery.StateOfCharge / BatteryInterval);
            var prevBucket = (int)((ev.Battery.StateOfCharge + batteryLost) / BatteryInterval);

            if (currentBucket != prevBucket)
                evsThatNeedChecking[evID] = evID;
        });

        foreach (var evID in evsThatNeedChecking)
            eventScheduler.ScheduleEvent(new CheckUrgency(evID, checkAndUpdateAllEVs.Time));

        var nextCheckTime = checkAndUpdateAllEVs.Time + intervalSize;
        var nextCheckEvent = new CheckAndUpdateAllEVs(nextCheckTime);
        eventScheduler.ScheduleEvent(nextCheckEvent);
    }

    private float CalculateStateOfCharge(EV ev, Time interval)
    {
        var waypoints = ev.Journey.Path.Waypoints;
        var totalDistance = 0.0d;
        for (var i = 0; i < waypoints.Count - 1; i++)
        {
            totalDistance += GeoMath.EquirectangularDistance(waypoints[i], waypoints[i + 1]);
        }

        var avgSpeed = totalDistance / ev.Journey.OriginalDuration;

        var totalDrivingTimeInInterval = interval * avgSpeed / 60;
        return (float)(totalDrivingTimeInInterval / 60) * ev.Efficiency;
    }
}
