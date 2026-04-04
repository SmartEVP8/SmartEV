namespace Engine.Events;

using Core.Charging;
using Core.Shared;
using Core.Vehicles;
using Engine.Cost;
using Engine.Events.Middleware;
using Engine.Routing;
using Engine.Vehicles;

/// <summary>
/// Handles the <see cref="FindCandidateStations"/> event by pre-computing candidate stations,
/// computing their costs, selecting the best station, and scheduling a <see cref="ReservationRequest"/> event.
/// </summary>
/// <param name="findCandidateStationService">Service for pre-computing candidate stations.</param>
/// <param name="computeCost">Cost computation service for selecting the best station.</param>
/// <param name="eventScheduler">Event scheduler for scheduling reservation requests.</param>
/// <param name="evStore">EV store for retrieving EV data.</param>
/// <param name="applyNewPath">To update a journey if a better one is found.</param>
public class FindCandidateStationsHandler(
    FindCandidateStationService findCandidateStationService,
    ComputeCost computeCost,
    EventScheduler eventScheduler,
    EVStore evStore,
    ApplyNewPath applyNewPath)
{
    /// <summary>
    /// Handles the <see cref="FindCandidateStations"/> event by pre-computing candidate stations.
    /// </summary>
    /// <param name="e">The event data.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(FindCandidateStations e)
    {
        var stationCosts = await findCandidateStationService.ComputeCandidateStationFromCache(e.EVId);
        ref var ev = ref evStore.Get(e.EVId);
        var bestStation = computeCost.Compute(ref ev, stationCosts, e.Time);

        if (ev.HasReservationAtStationId != null && ev.HasReservationAtStationId == bestStation.Id)
        {
            ev.Journey.GetCurrentPosition(e.Time);
            var remaining = ev.Journey.Current.EtaToNextStop - e.Time;
            if (ScheduleArriveAtStation(e, ev, bestStation, remaining))
                return;

            var nextCheckTime = e.Time + (remaining / 2);
            eventScheduler.ScheduleEvent(new FindCandidateStations(e.EVId, nextCheckTime));
            return;
        }

        if (ev.HasReservationAtStationId != null)
            eventScheduler.ScheduleEvent(new CancelRequest(e.EVId, ev.HasReservationAtStationId.Value, e.Time));

        bestStation.IncrementReservations();
        ev.HasReservationAtStationId = bestStation.Id;
        applyNewPath.ApplyNewPathToEV(ref ev, bestStation, e.Time);
        var timeToStation = ev.Journey.Current.DurationToNextStop;

        if (ScheduleArriveAtStation(e, ev, bestStation, timeToStation))
            return;

        var nextCheck = e.Time + (timeToStation / 2);
        eventScheduler.ScheduleEvent(new FindCandidateStations(e.EVId, nextCheck));
    }

    private bool ScheduleArriveAtStation(FindCandidateStations e, EV ev, Station bestStation, Time durationToStation)
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
}
