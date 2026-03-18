namespace Engine.Events;

public class EventDispatcher
{
    public void Dispatch(IEvent e)
    {
        switch (e)
        {
            case ReservationRequest rq:
                break;

            case CancelRequest cq:
                break;

            case ArriveAtStation aas:
                break;

            case StartCharging sc:
                break;

            case EndCharging ec:
                break;

            case ArriveAtDestination aad:
                break;

            default:
                throw new Exception("This should never happen, add a handler");
        }
    }
}
