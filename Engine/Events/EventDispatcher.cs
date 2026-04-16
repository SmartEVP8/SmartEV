namespace Engine.Events;

using Engine.Services;
using Engine.Utils;

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
/// <param name="snapshotEventHandler">Where the event <c>Snapshot</c> is handled.</param>
/// <param name="destinationArrivalHandler">Where the event <c>ArriveAtDestination</c> is handled.</param>
/// <param name="findCandidateStationsHandler">Where the event <c>FindCandidateStations</c> is handled.</param>
/// <param name="evService">Where the event <c>SpawnEVS</c> is handled.</param>
/// <param name="eventSubscriber">Optional subscriber to receive notifications of engine events.</param>
public class EventDispatcher(
        StationService stationService,
        SnapshotEventHandler snapshotEventHandler,
        FindCandidateStationsHandler findCandidateStationsHandler,
        EVService evService,
        DestinationArrivalHandler destinationArrivalHandler,
        IEngineEventSubscriber? eventSubscriber = null)
{
    private readonly IEngineEventSubscriber? _eventSubscriber = eventSubscriber;

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
            case ArriveAtStation ev:
                stationService.HandleArrivalAtStation(ev);
                _eventSubscriber?.OnArrivalAtStation(ev);
                break;

            case EndCharging ev:
                stationService.HandleEndCharging(ev);
                _eventSubscriber?.OnChargingEnd(ev);
                break;

            case FindCandidateStations ev:
                await findCandidateStationsHandler.Handle(ev);
                break;

            case ArriveAtDestination ev:
                destinationArrivalHandler.Handle(ev);
                break;

            case SnapshotEvent ev:
                snapshotEventHandler.Handle(ev);
                break;

            case SpawnEVS ev:
                evService.Handle(ev);
                break;

            default:
                throw Log.Error(0, e.Time, new SkillissueException("This should never happen, add a handler"), ("Event", e));
        }

        if (_eventCount % 1000 == 0)
            PrintCounts(e);
    }

    private readonly Dictionary<Type, uint> _calledCount = [];
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
}
