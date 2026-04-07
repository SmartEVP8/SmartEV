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
    IFindCandidateStationService findCandidateStationService,
    CostFunction costFunction,
    IEventScheduler eventScheduler,
    EVStore evStore,
    IEVDetourPlanner evDetourPlanner)
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

        if (candidateStationDurations.Count == 0)
        {
            HandleNoCandidates(e, ref ev);
            return;
        }

        var bestStation = costFunction.Compute(ref ev, candidateStationDurations, e.Time);

        if (HasScheduledReservationAtStation(e, ref ev, bestStation))
            return;

        ReplaceExistingReservation(e, ref ev, bestStation);
        ScheduleArrivalOrReschedule(e, ref ev, bestStation);
    }

    private void HandleNoCandidates(FindCandidateStations e, ref EV ev)
    {
        if (ev.HasReservationAtStationId is ushort reservedStationId)
        {
            var durationToStation = ev.Journey.Current.DurationToNextStop;
            eventScheduler.ScheduleEvent(new ArriveAtStation(
                e.EVId,
                reservedStationId,
                ev.CalcDesiredSoC(e.Time + durationToStation),
                e.Time + durationToStation));
            return;
        }

        throw new InvalidOperationException(
            $"No candidate stations available for EV {e.EVId} at {e.Time}. Reserved=null, SoC={ev.Battery.StateOfCharge:P2}, CurrentKWh={ev.Battery.CurrentChargeKWh:F2}, MinAcceptable={ev.Preferences.MinAcceptableCharge:P2}, RemainingToNextStop={ev.Journey.Current.DurationToNextStop}. Could have driven {(ev.Battery.CurrentChargeKWh - (ev.Battery.MaxCapacityKWh * ev.Preferences.MinAcceptableCharge)) / (ev.ConsumptionWhPerKm / 1000f):F1}km and had {ev.Journey.Current.DistanceKm:F2}km left.");
    }

    private bool HasScheduledReservationAtStation(FindCandidateStations e, ref EV ev, Station bestStation)
    {
        if (ev.HasReservationAtStationId == null || ev.HasReservationAtStationId != bestStation.Id)
            return false;

        ev.Advance(e.Time);
        var remaining = ev.Journey.Current.DurationToNextStop;

        if (HasScheduledArriveAtStation(e, ref ev, bestStation, remaining))
            return true;

        var nextTime = HalfwayToStation(remaining, e.Time);
        eventScheduler.ScheduleEvent(new FindCandidateStations(e.EVId, nextTime));
        return true;
    }

    private void ReplaceExistingReservation(FindCandidateStations e, ref EV ev, Station bestStation)
    {
        if (ev.HasReservationAtStationId != null)
            eventScheduler.ScheduleEvent(new CancelRequest(e.EVId, ev.HasReservationAtStationId.Value, e.Time));

        ev.HasReservationAtStationId = bestStation.Id;
        bestStation.IncrementReservations();
        evDetourPlanner.Update(ref ev, bestStation, e.Time);
    }

    private void ScheduleArrivalOrReschedule(FindCandidateStations e, ref EV ev, Station bestStation)
    {
        var remainingTravelTime = ev.Journey.Current.DurationToNextStop;
        if (HasScheduledArriveAtStation(e, ref ev, bestStation, remainingTravelTime))
            return;

        var nextTime = HalfwayToStation(remainingTravelTime, e.Time);
        eventScheduler.ScheduleEvent(new FindCandidateStations(e.EVId, nextTime));
    }

    private bool HasScheduledArriveAtStation(FindCandidateStations e, ref EV ev, Station bestStation, Time durationToStation)
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
