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
    /// <param name="checkAndUpdateAllEVs">The event for checking and updating all EVs.</param>
    public void Handle(CheckAndUpdateAllEVs checkAndUpdateAllEVs)
    {
        var evsThatNeedChecking = new int[evStore.Count];
        Array.Fill(evsThatNeedChecking, -1);

        Parallel.For(0, evStore.Count, evID =>
        {
            ref var ev = ref evStore.Get(evID);

            var currentTime = eventScheduler.CurrentTime;
            if (ev.Journey is null || ev.IsCharging || !ev.HasDeparted(currentTime) || ev.HasArrived(currentTime)) return;
            Console.WriteLine($"Checking EV {evID} at time {currentTime}");
            var batteryLost = CalculateStateOfCharge(ev, intervalSize);
            var socLost = batteryLost / ev.Battery.Capacity * 100;
            Console.WriteLine($"EV {evID} lost {socLost}% SoC in the last interval, current SoC: {ev.Battery.StateOfCharge}%");
            ev.Battery.StateOfCharge -= socLost;
            Console.WriteLine($"EV {evID} new SoC: {ev.Battery.StateOfCharge}% \n");
            var currentBucket = (int)(ev.Battery.StateOfCharge / BatteryInterval);
            var prevBucket = (int)((ev.Battery.StateOfCharge + socLost) / BatteryInterval);
            //Console.WriteLine($"EV {evID} lost {socLost}% SoC, current SoC: {ev.Battery.StateOfCharge}%, bucket: {currentBucket}, previous bucket: {prevBucket}");
            if (currentBucket != prevBucket)
                evsThatNeedChecking[evID] = evID;
        });

        foreach (var evID in evsThatNeedChecking)
        {
            if (evID == -1) continue;
            eventScheduler.ScheduleEvent(new CheckUrgency(evID, checkAndUpdateAllEVs.Time));
        }

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
            totalDistance +=
                GeoMath.EquirectangularDistance(waypoints[i], waypoints[i + 1]);
        }

        var distanceInInterval =
            totalDistance * (interval / (double)ev.Journey.OriginalDuration);
        Console.WriteLine($"totalDistance: {totalDistance}km, duration: {ev.Journey.OriginalDuration}s, interval: {interval}s, efficiency: {ev.Efficiency}Wh/km, capacity: {ev.Battery.Capacity}kWh");
        return (float)(distanceInInterval * ev.Efficiency / 1000.0);
    }
}
