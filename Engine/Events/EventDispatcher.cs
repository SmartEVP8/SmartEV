namespace Engine.Events;

using Engine.Services;

/// <summary>
/// The EventDispatcher is responsible for dispatching events to the correct handlers.
/// </summary>
public class EventDispatcher(
        StationService stationService,
        CheckUrgencyHandler checkUrgencyHandler,
        SnapshotEventHandler snapshotEventHandler,
        EVService evService,
        DestinationArrivalHandler destinationArrivalHandler)
{
    /// <summary>
    /// Dispatches the event to the correct handler.
    /// Has a handler for every <c>Event</c>. If an event is dispatched for which there is no handler, an exception is thrown.
    /// </summary>
    /// <param name="e">The event to handle.</param>
    /// <exception cref="Exception">If a event handler is not registered.</exception>
    public void Dispatch(Event e)
    {
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
                // TODO: Cost function here
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

            default:
                throw new Exception("This should never happen, add a handler");
        }
    }
}
