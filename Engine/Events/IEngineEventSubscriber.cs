namespace Engine.Events;

/// <summary>
/// Interface for subscribing to Engine events from outside the Engine domain.
/// Used by SimulationEngineService to translate Engine events to protocol events.
/// </summary>
public interface IEngineEventSubscriber
{
    void OnArrivalAtStation(ArriveAtStation @event);

    void OnChargingEnd(EndCharging @event);
}
