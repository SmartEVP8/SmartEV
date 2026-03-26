namespace Engine.Events;

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
        findCandidateStationService.PreComputeCandidateStation()(e);

        _ = Task.Run(async () =>
        {
            var stationCosts = await findCandidateStationService.ComputeCandidateStationFromCache(e.EVId);

            if (stationCosts.Count == 0)
                return;

            var ev = evStore.Get(e.EVId);
            var stations = stationCosts.Keys.ToArray();

            var journeys = (
                duration: stationCosts.Values.ToArray(),
                distance: new float[stations.Length]);

            var bestStation = computeCost.Compute(ref ev, stations, journeys);
            var reservationRequest = new ReservationRequest(e.EVId, bestStation.Id, e.Time);
            eventScheduler.ScheduleEvent(reservationRequest);
        });
    }
}
