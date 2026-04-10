namespace Engine.Events;

/// <summary>
/// Interface for subscribing to Engine events from outside the Engine domain.
/// Used by SimulationEngineService to translate Engine events to protocol events.
/// </summary>
public interface IEngineEventSubscriber
{
    /// <summary>
    /// Handles the arrival at station event by converting it to a protocol event and sending it to the client.
    /// </summary>    
    /// <param name="event">The arrival at station event from the engine.</param>
    void OnArrivalAtStation(ArriveAtStation @event);

    /// <summary>
    /// Handles the end of charging event by converting it to a protocol event and sending it to the client.
    /// </summary>
    /// <param name="event">The end of charging event from the engine.</param>
    void OnChargingEnd(EndCharging @event);
}
