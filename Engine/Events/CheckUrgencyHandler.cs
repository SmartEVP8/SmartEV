using Core.Shared;
namespace Engine.Events;

using Core.Vehicles;
using Engine.Vehicles;
using Engine.GeoMath;

public class CheckUrgencyHandler(EventScheduler eventScheduler, EVStore evStore, int intervalSize, Random random)
{
    /// <summary>
    /// Handles the CheckUrgency event by calculating the urgency of an EV's charge and scheduling a FindCandidate event if necessary.
    /// It also schedules the next CheckUrgency event based on the EV's current state of charge and journey.
    /// </summary>
    /// <param name="checkUrgency">The event for checking urgency of an EV.</param>
    public void Handle(CheckUrgency checkUrgency)
    {
        var ev = evStore.Get(checkUrgency.EVId);
        if (ev.IsCharging)
        {
            var urgency = Urgency.CalculateChargeUrgency(ev.Battery.StateOfCharge, ev.Preferences.MinAcceptableCharge);
            if (urgency == 1)
            {
                var findCandidateEvent = new FindCandidateStations(checkUrgency.EVId, checkUrgency.Time);
                eventScheduler.ScheduleEvent(findCandidateEvent);
            }
            else if (urgency > 0.0)
            {
                var randomPercentage = random.NextDouble();
                if (urgency >= randomPercentage)
                {
                    var findCandidateEvent = new FindCandidateStations(checkUrgency.EVId, checkUrgency.Time);
                    eventScheduler.ScheduleEvent(findCandidateEvent);
                }
            }

            var newCheckUrgency = new CheckUrgency(checkUrgency.EVId, checkUrgency.Time + NextTimeToCheck(ev));
            eventScheduler.ScheduleEvent(newCheckUrgency);
        }
    }

    private Time NextTimeToCheck(EV ev)
    {
        var waypoints = ev.Journey.Path.Waypoints;
        var sumOfPath = waypoints.Zip(waypoints.Skip(1), (a, b) => GeoMath.EquirectangularDistance(a, b)).Sum();

        var totalLengthOnFullBattery = ev.Battery.Capacity / ev.Efficiency * 100;
        var avgSpeed = sumOfPath / ev.Journey.OriginalDuration;
        var totalDurationOnFullBattery = totalLengthOnFullBattery / avgSpeed;

        var nextCheck = (ev.Battery.StateOfCharge / ev.Battery.Capacity) % intervalSize;
        return (Time)((totalDurationOnFullBattery / 100) * nextCheck);
    }
}
