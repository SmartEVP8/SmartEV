namespace Engine.Events;

using Core.Charging;
using Core.Shared;
using Core.Vehicles;
using Engine.Cost;
using Engine.Events.Middleware;
using Engine.Routing;
using Engine.Vehicles;

/// <summary>
/// Event handler for finding candidate stations for an EV. 
/// Reschedules itself for every halfway point its station.
/// If a better station is found, it cancels the existing reservation and creates a new one at the better station.
/// </summary>
/// <param name="findCandidateStationService">Service for pre-computing candidate stations.</param>
/// <param name="costFunction">Cost computation service for selecting the best station.</param>
/// <param name="eventScheduler">Event scheduler for scheduling reservation requests.</param>
/// <param name="evStore">EV store for retrieving EV data.</param>
/// <param name="evDetourPlanner">To update a journey if a better one is found.</param>
public class FindCandidateStationsHandler(
    FindCandidateStationService findCandidateStationService,
    CostFunction costFunction,
    EventScheduler eventScheduler,
    EVStore evStore,
    EVDetourPlanner evDetourPlanner)
{
    /// <summary>
    /// Handles the <see cref="FindCandidateStations"/> event by pre-computing candidate stations.
    /// </summary>
    /// <param name="e">The event data.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(FindCandidateStations e)
    {
        var candidateStationDurations = await findCandidateStationService.GetCandidateStationFromCache(e.EVId);
        ref var ev = ref evStore.Get(e.EVId);
        var bestStation = costFunction.Compute(ref ev, candidateStationDurations, e.Time);

        if (HasScheduledReservationAtStation(e, ev, bestStation))
            return;

        ReplaceExistingReservation(e, ev, bestStation);
        ScheduleArrivalOrReschedule(e, ev, bestStation);
    }

    private bool HasScheduledReservationAtStation(FindCandidateStations e, EV ev, Station bestStation)
    {
        if (ev.HasReservationAtStationId == null || ev.HasReservationAtStationId != bestStation.Id)
            return false;

        ev.Advance(e.Time);
        var remaining = ev.Journey.Current.DurationToNextStop;

        if (HasScheduledArriveAtStation(e, ev, bestStation, remaining))
            return true;

        eventScheduler.ScheduleEvent(new FindCandidateStations(e.EVId, HalfwayToStation(remaining, e.Time)));
        return true;
    }

    private void ReplaceExistingReservation(FindCandidateStations e, EV ev, Station bestStation)
    {
        if (ev.HasReservationAtStationId != null)
            eventScheduler.ScheduleEvent(new CancelRequest(e.EVId, ev.HasReservationAtStationId.Value, e.Time));

        ev.HasReservationAtStationId = bestStation.Id;
        bestStation.IncrementReservations();
        evDetourPlanner.Update(ref ev, bestStation, e.Time);
    }

    private void ScheduleArrivalOrReschedule(FindCandidateStations e, EV ev, Station bestStation)
    {
        var remainingTravelTime = ev.Journey.Current.DurationToNextStop;
        if (HasScheduledArriveAtStation(e, ev, bestStation, remainingTravelTime))
            return;

        eventScheduler.ScheduleEvent(new FindCandidateStations(e.EVId, HalfwayToStation(remainingTravelTime, e.Time)));
    }

    private bool HasScheduledArriveAtStation(FindCandidateStations e, EV ev, Station bestStation, Time durationToStation)
    {
        if (durationToStation <= 60 * 10)
        {
            eventScheduler.ScheduleEvent(new ArriveAtStation(
            e.EVId,
            bestStation.Id,
            ev.CalcDesiredSoC(e.Time + durationToStation),
            e.Time + durationToStation));
            return true;
        }

        return false;
    }

    private static Time HalfwayToStation(Time remaining, Time currentTime) => currentTime + (remaining / 2);
}
