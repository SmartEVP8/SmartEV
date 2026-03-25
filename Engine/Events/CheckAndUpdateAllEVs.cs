
namespace Engine.Events;

using Engine.Vehicles;
using Engine.GeoMath;
using Core.Shared;
using Core.Vehicles;
public class CheckAndUpdateAllEVsHandler(EventScheduler eventScheduler, EVStore evStore, Time intervalSize, ushort BatteryInterval)
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
            if (ev.Journey is not null && !ev.IsCharging)
            {
                var batteryLost = CalculateStateOfCharge(ev, intervalSize);
                ev.Battery.StateOfCharge = ev.Battery.StateOfCharge - batteryLost;
                if (batteryLost > BatteryInterval || (ev.Battery.StateOfCharge % BatteryInterval) < BatteryInterval << 4)
                {
                    evsThatNeedChecking[evID] = evID;
                }
            }
        });
        foreach (var evID in evsThatNeedChecking)
        {
            if (evID == 0) continue;
            eventScheduler.ScheduleEvent(new CheckUrgency(evID, checkAndUpdateAllEVs.Time));
        }

        var nextCheckTime = checkAndUpdateAllEVs.Time + intervalSize;
        var nextCheckEvent = new CheckAndUpdateAllEVs(nextCheckTime);
        eventScheduler.ScheduleEvent(nextCheckEvent);
    }

    public float CalculateStateOfCharge(EV ev, Time interval)
    {
        var waypoints = ev.Journey.Path.Waypoints;
        var totalDistance = 0.0d;
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            totalDistance += GeoMath.EquirectangularDistance(waypoints[i], waypoints[i + 1]);
        }

        var avgSpeed = totalDistance / ev.Journey.OriginalDuration;

        var totalDrivingTimeInInterval = (interval / 60) * avgSpeed;
        return (float)(totalDrivingTimeInInterval / 60) * ev.Efficiency;

    }
}
