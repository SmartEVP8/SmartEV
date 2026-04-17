namespace Engine.Events;

using Core.Charging;
using Core.Shared;
using Core.Vehicles;
using Engine.Cost;
using Engine.Routing;
using Engine.Services;
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
/// <param name="stationService">To check for ev reservations.</param>
public class FindCandidateStationsHandler(
    IFindCandidateStationService findCandidateStationService,
    CostFunction costFunction,
    IEventScheduler eventScheduler,
    EVStore evStore,
    IEVDetourPlanner evDetourPlanner,
    StationService stationService)
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
        ev.Advance(e.Time);

        if (candidateStationDurations.Count == 0)
        {
            HandleNoCandidates(e, ref ev);
            return;
        }

        var bestStation = costFunction.Compute(ref ev, candidateStationDurations, e.Time);

        if (stationService.GetReservationStationId(e.EVId) != bestStation.Id)
            evDetourPlanner.Update(ref ev, bestStation, e.Time);

        var remaining = ev.Journey.Current.DurationToNextStop;
        var etaAtStation = e.Time + remaining;
        var targetSoC = ev.CalcDesiredSoC(etaAtStation);
        var socAtArrival = ev.EstimateSoCAtNextStop();

        if (ev.Battery.StateOfCharge >= targetSoC)
        {
            throw new InvalidOperationException(
                $"EV {e.EVId} is attempting charger planning despite already being above target SoC. " +
                $"CurrentSoC={ev.Battery.StateOfCharge}, TargetSoC={targetSoC}, Time={e.Time}.");
        }

        if (socAtArrival >= targetSoC)
        {
            throw new InvalidOperationException(
                $"EV {e.EVId} is attempting charger planning despite being projected to arrive above target SoC. " +
                $"CurrentSoC={ev.Battery.StateOfCharge}, SoCAtArrival={socAtArrival}, TargetSoC={targetSoC}, Time={e.Time}.");
        }

        stationService.HandleReservation(new Reservation(e.EVId, etaAtStation, socAtArrival, targetSoC), bestStation.Id);

        if (remaining <= Time.MillisecondsPerMinute * 10)
        {
            eventScheduler.ScheduleEvent(new ArriveAtStation(e.EVId, bestStation.Id, targetSoC, etaAtStation));
            return;
        }

        eventScheduler.ScheduleEvent(new FindCandidateStations(e.EVId, ev.TimeToNextFindCandidateCheck(e.Time)));
    }

    private void HandleNoCandidates(FindCandidateStations e, ref EV ev)
    {
        if (stationService.GetReservationStationId(e.EVId) is ushort reservedStationId)
        {
            ev.Advance(e.Time);
            var durationToStation = ev.Journey.Current.DurationToNextStop;

            eventScheduler.ScheduleEvent(new ArriveAtStation(
                e.EVId,
                reservedStationId,
                ev.CalcDesiredSoC(e.Time + durationToStation),
                e.Time + durationToStation));
            return;
        }

        throw new InvalidOperationException($"No candidate stations available for EV {e.EVId} at {e.Time}.");
    }
}
