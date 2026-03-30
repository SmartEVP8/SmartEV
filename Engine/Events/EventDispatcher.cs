namespace Engine.Events;

using Engine.Services;

/// <summary>
/// The EventDispatcher is responsible for dispatching events to the correct handlers.
/// It has a handler for every <c>Event</c>. If an event is dispatched for which there is no handler, an exception is thrown.
/// </summary>
/// <param name="stationService">
/// The service where the events
/// <c>ReservationRequest</c>,
/// <c>CancelRequest</c>,
/// <c>ArriveAtStation</c>,
/// and <c>EndCharging</c> are handled.
/// </param>
/// <param name="checkUrgencyHandler">Where the event <c>CheckUrgency</c> is handled.</param>
/// <param name="snapshotEventHandler">Where the event <c>Snapshot</c> is handled.</param>
/// <param name="destinationArrivalHandler">Where the event <c>ArriveAtDestination</c> is handled.</param>
/// <param name="findCandidateStationsHandler">Where the event <c>FindCandidateStations</c> is handled.</param>
/// <param name="evService">Where the event <c>SpawnEVS</c> is handled.</param>
/// <param name="CheckAndUpdateAllEVsHandler">Where the event <c>CheckAndUpdateAllEVs</c> is handled.</param>
public class EventDispatcher(
        StationService stationService,
        CheckUrgencyHandler checkUrgencyHandler,
        SnapshotEventHandler snapshotEventHandler,
        FindCandidateStationsHandler findCandidateStationsHandler,
        EVService evService,
        DestinationArrivalHandler destinationArrivalHandler,
        CheckAndUpdateEVHandler CheckAndUpdateAllEVsHandler)
{
    private Dictionary<Type, uint> _calledCount = [];
    private int _eventCount;

    private void IncrementCount(Event e)
    {
        var type = e.GetType();

        if (_calledCount.ContainsKey(type))
        {
            _calledCount[type]++;
        }
        else
        {
            _calledCount[type] = 1;
        }

        _eventCount++;
    }

    private void PrintCounts(Event e)
    {
        Console.WriteLine($"Event counts in timestamp {e.Time}:");
        foreach (var kvp in _calledCount)
        {
            Console.WriteLine($"{kvp.Key.Name}: {kvp.Value}");
        }
    }

    /// <summary>
    /// Dispatches the event to the correct handler.
    /// Has a handler for every <c>Event</c>. If an event is dispatched for which there is no handler, an exception is thrown.
    /// </summary>
    /// <param name="e">The event to handle.</param>
    /// <exception cref="Exception">If a event handler is not registered.</exception>
    /// <returns>What return?.</returns>
    public async Task Dispatch(Event e)
    {
        IncrementCount(e);
        switch (e)
        {
            case ReservationRequest ev:
                stationService.HandleReservationRequest(ev);
                break;

            case CancelRequest ev:
                stationService.HandleCancelRequest(ev);
                break;

            case ArriveAtStation ev:
                stationService.HandleArrivalAtStation(ev);
                break;

            case EndCharging ev:
                stationService.HandleEndCharging(ev);
                break;

            case FindCandidateStations ev:
                await findCandidateStationsHandler.Handle(ev);
                break;

            case ArriveAtDestination ev:
                destinationArrivalHandler.Handle(ev);
                break;

            case CheckUrgency ev:
                checkUrgencyHandler.Handle(ev);
                break;

            case SnapshotEvent ev:
                snapshotEventHandler.Handle(ev);
                break;

            case SpawnEVS ev:
                evService.Handle(ev);
                break;

            case CheckAndUpdateEV ev:
                CheckAndUpdateAllEVsHandler.Handle(ev);
                break;

            default:
                throw new Exception("This should never happen, add a handler");
        }

        if (_eventCount % 1000 == 0)
            PrintCounts(e);
    }
}
