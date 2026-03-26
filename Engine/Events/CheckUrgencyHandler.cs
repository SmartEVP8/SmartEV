namespace Engine.Events;

using Core.Shared;
using Core.Vehicles;
using Engine.Vehicles;

/// <summary>
/// Handles the CheckUrgency event by calculating the urgency of an
/// EV's charge and scheduling a FindCandidate event if necessary.
/// </summary>
/// <param name="eventScheduler">The event scheduler used to schedule events.</param>
/// <param name="evStore">The store containing information about electric vehicles.</param>
/// <param name="random">The random number generator used for probabilistic decisions.</param>
public class CheckUrgencyHandler(EventScheduler eventScheduler, EVStore evStore, Random random)
{
    /// <summary>
    /// Handles the CheckUrgency event by calculating the urgency of an EV's charge and scheduling a FindCandidate event if necessary.
    /// It also schedules the next CheckUrgency event based on the EV's current state of charge and journey.
    /// </summary>
    /// <param name="checkUrgency">The event for checking urgency of an EV.</param>
    public void Handle(CheckUrgency checkUrgency)
    {
        var ev = evStore.Get(checkUrgency.EVId);

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
    }
}
