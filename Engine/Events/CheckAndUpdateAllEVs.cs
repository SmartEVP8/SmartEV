namespace Engine.Events;

using Engine.Vehicles;
using Core.GeoMath;
using Core.Shared;
using Core.Vehicles;

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

        for (var evID = 0; evID < evStore.Count; evID++)
        {
            ref var ev = ref evStore.Get(evID);

            var currentTime = eventScheduler.CurrentTime;
            if (ev.Journey is null || ev.IsCharging || !ev.HasDeparted(currentTime) || ev.HasArrived(currentTime)) continue;

            var batteryLost = CalculateStateOfCharge(ev, intervalSize);
            var socLost = batteryLost / ev.Battery.MaxCapacityKWh * 100;

            ev.Battery.StateOfCharge -= socLost;

            var currentBucket = (int)(ev.Battery.StateOfCharge / BatteryInterval);
            var prevBucket = (int)((ev.Battery.StateOfCharge + socLost) / BatteryInterval);

            if (currentBucket != prevBucket)
                evsThatNeedChecking[evID] = evID;
        }

        foreach (var evID in evsThatNeedChecking)
        {
            if (evID == -1) continue;
            eventScheduler.ScheduleEvent(new CheckUrgency(evID, e.Time));
        }

        var nextCheckTime = e.Time + intervalSize;
        var nextCheckEvent = new CheckAndUpdateAllEVs(nextCheckTime);
        eventScheduler.ScheduleEvent(nextCheckEvent);
    }

    private float CalculateStateOfCharge(EV ev, Time interval)
    {
        var waypoints = ev.Journey.Path.Waypoints;
        var totalDistancekm = 0.0d;
        for (var i = 0; i < waypoints.Count - 1; i++)
        {
            totalDistancekm +=
                GeoMath.EquirectangularDistance(waypoints[i], waypoints[i + 1]);
        }

        var distanceInInterval =
            totalDistancekm * (interval / (double)ev.Journey.LastUpdatedDuration);

        return (float)(distanceInInterval * ev.ConsumptionWhPerKm / 1000.0);
    }
}
