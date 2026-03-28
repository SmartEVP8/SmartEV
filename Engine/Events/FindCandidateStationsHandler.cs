namespace Engine.Events;

using Core.Shared;
using Engine.Cost;
using Engine.Events.Middleware;
using Engine.Vehicles;

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
    EVStore evStore)
{
    /// <summary>
    /// Handles the <see cref="FindCandidateStations"/> event by pre-computing candidate stations,
    /// computing their costs, selecting the best station, and scheduling a <see cref="ReservationRequest"/> event.
    /// </summary>
    /// <param name="e">The <see cref="FindCandidateStations"/> event.</param>
    public void Handle(FindCandidateStations e)
    {
        var stationCosts = findCandidateStationService.GetCandidateStations(e);
        var ev = evStore.Get(e.EVId);
        var stations = stationCosts.Keys.ToArray();
        var journeyDurations = stationCosts.Values.ToArray();

        var bestStation = computeCost.Compute(ref ev, stations, journeyDurations);
        var durationToStation = Math.Ceiling(stationCosts[bestStation]);
        var reservationRequest = new ReservationRequest(e.EVId, bestStation.Id, e.Time, (Time)(uint)durationToStation);
        eventScheduler.ScheduleEvent(reservationRequest);
    }
}
