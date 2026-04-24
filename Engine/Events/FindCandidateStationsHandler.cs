namespace Engine.Events;

using Core.Charging;
using Core.Shared;
using Core.Vehicles;
using Engine.Cost;
using Engine.Routing;
using Engine.Services;
using Engine.Utils;
using Engine.Vehicles;
using Core.Helper;

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
        var candidateStations = await findCandidateStationService.GetCandidateStationFromCache(e.EVId);
        ref var ev = ref evStore.Get(e.EVId);
        ev.Advance(e.Time);

        if (ev.Battery.StateOfCharge >= 0.7f)
        {
            var nextCheckTime = ev.TimeAtNextFindCandidateCheck(e.Time);
            Log.Info(e.EVId, e.Time, $"EV {e.EVId} has more than 70% SoC at find candidate stations. Rescheduling next check at {nextCheckTime}.");
            eventScheduler.ScheduleEvent(new FindCandidateStations(e.EVId, nextCheckTime));
            return;
        }

        Log.Verbose(e.EVId, e.Time, $"Handling FindCandidateStations for EV {e.EVId} at time {e.Time}. Current EV data: {ev}. SoC: {ev.Battery.StateOfCharge}, Next stop in {ev.Journey.Current.DurationToNextStop}ms.)", ("Journey", ev.Journey));
        if (candidateStations.Count == 0)
        {
            HandleNoCandidates(e, ref ev);
            return;
        }

        var bestStation = costFunction.Compute(ref ev, candidateStations, e.Time) ?? throw Log.Error(e.EVId, e.Time, new SkillissueException("Cost function did not return a station, but should never get this far."));

        if (stationService.GetReservationStationId(e.EVId) != bestStation.Id)
            evDetourPlanner.Update(ref ev, bestStation, e.Time);

        var remaining = ev.Journey.Current.DurationToNextStop;
        var etaAtStation = e.Time + remaining;
        var targetSoC = ev.CalcDesiredSoC(etaAtStation);
        var socAtArrival = ev.EstimateSoCAtNextStop();
        stationService.HandleReservation(new Reservation(e.EVId, etaAtStation, socAtArrival, targetSoC), bestStation.Id);

        if (remaining <= Time.MillisecondsPerMinute * 10)
        {
            Log.Info(e.EVId, e.Time, $"EV {e.EVId} is close to station {bestStation.Id} with ETA {etaAtStation} and SoC at arrival {socAtArrival} with a current SoC of {ev.Battery.StateOfCharge}. Making arrival at station event immediately.");
            eventScheduler.ScheduleEvent(new ArriveAtStation(e.EVId, bestStation.Id, targetSoC, etaAtStation));
            return;
        }

        eventScheduler.ScheduleEvent(new FindCandidateStations(e.EVId, ev.TimeAtNextFindCandidateCheck(e.Time)));
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

        throw Log.Error(e.EVId, e.Time, new InvalidOperationException($"No candidate stations available for EV {e.EVId} at {e.Time}."));
    }
}
