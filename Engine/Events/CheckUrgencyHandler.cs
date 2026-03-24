namespace Engine.Events;

using Core.Vehicles;
using Engine.Vehicles;
using Engine.GeoMath;

public class CheckUrgencyHandler(EventScheduler eventScheduler, EVStore evStore, int IntervalSize)
{
    private readonly EventScheduler _eventScheduler = eventScheduler;
    private readonly EVStore _evStore = evStore;
    private readonly int _intervalSize = IntervalSize;

    /// <summary>
    /// Handles the CheckUrgency event by calculating the urgency of an EV's charge and scheduling a FindCandidate event if necessary.
    /// It also schedules the next CheckUrgency event based on the EV's current state of charge and journey.
    /// </summary>
    /// <param name="checkUrgency">The event for checking urgency of an EV.</param>
    public void Handle(CheckUrgency checkUrgency)
    {
        var ev = _evStore.Get(checkUrgency.EVId);
        var urgency = Urgency.CalculateChargeUrgency(ev.Battery.StateOfCharge, ev.Preferences.MinAcceptableCharge);
        if (urgency == 1)
        {
            var findCandidateEvent = new FindCandidate(checkUrgency.EVId, checkUrgency.Time);
            _eventScheduler.ScheduleEvent(findCandidateEvent);
        }
        else if (urgency > 0.0)
        {
            var randomPercentage = new Random().NextDouble();
            if (urgency >= randomPercentage)
            {
                var findCandidateEvent = new FindCandidate(checkUrgency.EVId, checkUrgency.Time);
                _eventScheduler.ScheduleEvent(findCandidateEvent);
            }
        }

        var newCheckUrgency = new CheckUrgency(checkUrgency.EVId, checkUrgency.Time + NextTimeToCheck(ev));
        _eventScheduler.ScheduleEvent(newCheckUrgency);
    }

    private uint NextTimeToCheck(EV ev)
    {
        var waypoints = ev.Journey.Path.Waypoints;
        var sumOfPath = waypoints.Zip(waypoints.Skip(1), (a, b) => GeoMath.EquirectangularDistance(a, b)).Sum();

        var totalLengthOnFullBattery = ev.Battery.Capacity / ev.Efficiency * 100;
        var avgSpeed = sumOfPath / ev.Journey.OriginalDuration;
        var totalDurationOnFullBattery = totalLengthOnFullBattery / avgSpeed;

        var nextCheck = (ev.Battery.StateOfCharge / ev.Battery.Capacity) % _intervalSize;
        return (uint)((totalDurationOnFullBattery / 100) * nextCheck);
    }
}
