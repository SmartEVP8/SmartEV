using Core.Routing;
namespace Engine.Events;

using Core.Shared;
using Engine.Cost;
using Engine.Events.Middleware;
using Engine.Vehicles;
using Engine.Services;
using Engine.Routing;
using Core.Charging;
using Core.Vehicles;

/// <summary>
/// Handles the <see cref="FindCandidateStations"/> event by pre-computing candidate stations,
/// computing their costs, selecting the best station, and scheduling a <see cref="ReservationRequest"/> event.
/// </summary>
/// <param name="findCandidateStationService">Service for pre-computing candidate stations.</param>
/// <param name="computeCost">Cost computation service for selecting the best station.</param>
/// <param name="eventScheduler">Event scheduler for scheduling reservation requests.</param>
/// <param name="evStore">EV store for retrieving EV data.</param>
public class FindCandidateStationsHandler(
    FindCandidateStationService findCandidateStationService,
    ComputeCost computeCost,
    EventScheduler eventScheduler,
    EVStore evStore,
    ApplyNewPath applyNewPath)
{
    private uint _numberOfNoStations = 0;

    /// <summary>
    /// Case 1: The first time the function is called we choose the best station among candidates and reserve a spot.
    /// Case 2 (Base): We have a reserveration from Case 1 and conclude it's the best one still.
    /// Case 3 (Recursive): We have a reservation from Case 1 and conclude it's the wrong one.
    /// </summary>
    /// <param name="e">The event data.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Handle(FindCandidateStations e)
    {

        ref var ev = ref evStore.Get(e.EVId);
        var stationCosts = await findCandidateStationService.GetCandidateStationFromCache(e.EVId);

        if (stationCosts.Count == 0)
        {
            _numberOfNoStations++;
            Console.WriteLine($"[EV {e.EVId}] has no stations. {ev}. Total number of failed EV's {_numberOfNoStations}");
            evStore.Free(e.EVId);
            return;
        }

        var bestStation = computeCost.Compute(ref ev, stationCosts, e.Time);
        var durationToStation = Math.Ceiling(stationCosts[bestStation.Id]);

        // Case 1.
        if (ev.HasReservationAtStationId is null)
        {
            RescheduleDecision(bestStation, durationToStation, ref ev, e);
            return;
        }

        // Case 2.
        if (ev.HasReservationAtStationId == bestStation.Id)
        {
            ev.ConsumeEnergy(ev.Journey.LastUpdatedDeparture, e.Time);
            return;
        }

        // Case 3.
        ev.ConsumeEnergy(ev.Journey.LastUpdatedDeparture, e.Time);
        eventScheduler.ScheduleEvent(new CancelRequest(e.EVId, (ushort)ev.HasReservationAtStationId, e.Time));
        RescheduleDecision(bestStation, durationToStation, ref ev, e);
    }

    private void RescheduleDecision(Station bestStation, double durationToStation, ref EV ev, FindCandidateStations e)
    {
        bestStation.IncrementReservations();
        var arriveAtStation = new ArriveAtStation(e.EVId, bestStation.Id, ev.CalcDesiredSoC((Time)(uint)durationToStation), (Time)(uint)durationToStation);
        eventScheduler.ScheduleEvent(arriveAtStation);

        var decisionPoint = applyNewPath.ApplyNewPathToEV(ref ev, bestStation, e.Time);
        var decisionTime = ev.Journey.DurationToWayPoint(decisionPoint);
        eventScheduler.ScheduleEvent(new FindCandidateStations(e.EVId, decisionTime));
    }
}
