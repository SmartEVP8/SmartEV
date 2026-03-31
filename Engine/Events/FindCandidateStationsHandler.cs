namespace Engine.Events;

using Core.Shared;
using Engine.Cost;
using Engine.Events.Middleware;
using Engine.Vehicles;
using Engine.Services;
using Engine.Routing;

/// <summary>
/// Handles the <see cref="FindCandidateStations"/> event by pre-computing candidate stations,
/// computing their costs, selecting the best station, and scheduling a <see cref="ReservationRequest"/> event.
/// </summary>
/// <param name="findCandidateStationService">Service for pre-computing candidate stations.</param>
/// <param name="computeCost">Cost computation service for selecting the best station.</param>
/// <param name="eventScheduler">Event scheduler for scheduling reservation requests.</param>
/// <param name="evStore">EV store for retrieving EV data.</param>
/// <param name="stationService">Station service for retrieving station data.</param>
public class FindCandidateStationsHandler(
    FindCandidateStationService findCandidateStationService,
    ComputeCost computeCost,
    EventScheduler eventScheduler,
    EVStore evStore,
    IStationService stationService,
    ApplyNewPath applyNewPath)
{
    private uint _numberOfNoStations = 0;

    /// <summary>
    /// Handles the <see cref="FindCandidateStations"/> event by pre-computing candidate stations.
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

        if (ev.HasReservationAtStationId is not null)
        {
            if (ev.HasReservationAtStationId == bestStation.Id)
                return;

            eventScheduler.ScheduleEvent(new CancelRequest(e.EVId, (ushort)ev.HasReservationAtStationId, e.Time));
        }

        bestStation.IncrementReservations();

        var oldPath = ev.Journey.Path;

        applyNewPath.ApplyNewPathToEV(ref ev, bestStation, e.Time);

        var newPath = ev.Journey.Path;

        var durationToStation = Math.Ceiling(stationCosts[bestStation.Id]);

        var arriveAtStation = new ArriveAtStation(e.EVId, bestStation.Id, ev.CalcDesiredSoC((Time)(uint)durationToStation), (Time)(uint)durationToStation);
        eventScheduler.ScheduleEvent(arriveAtStation);
    }
}
