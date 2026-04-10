namespace API.Services;

using Engine.Events;
using Protocol;

/// <summary>
/// Bridges engine events to protocol events and sends them to the connected client.
/// </summary>
public sealed class EngineEventSubscriber(
    IEventSender eventSender,
    ILogger<EngineEventSubscriber> logger) : IEngineEventSubscriber
{
    private readonly ILogger<EngineEventSubscriber> _logger = logger;

    /// <summary>
    /// Handles the arrival at station event by converting it to a protocol event and sending it to the client.
    /// </summary>
    /// <param name="event">The arrival at station event from the engine.</param>
    public async void OnArrivalAtStation(ArriveAtStation @event)
    {
        try
        {
            var protocolEvent = new ArrivalEvent
            {
                StationId = @event.StationId,
                EvId = @event.EVId,
                TimestampMs = (ulong)@event.Time.T * 1000,
            };

            var envelope = new Envelope { Arrival = protocolEvent };
            await eventSender.SendAsync(envelope);
            _logger.LogDebug("EV {EvId} arrived at station {StationId}", @event.EVId, @event.StationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling ArrivalAtStation event");
        }
    }

    /// <summary>
    /// Handles the end of charging event by converting it to a protocol event and sending it to the client.
    /// </summary>
    /// <param name="event">The end of charging event from the engine.</param> <summary>
    /// 
    /// </summary>
    /// <param name="event"></param>
    public async void OnChargingEnd(EndCharging @event)
    {
        try
        {
            var protocolEvent = new ChargingEndEvent
            {
                StationId = @event.StationId,
                EvId = @event.EVId,
                TimestampMs = (ulong)@event.Time.T * 1000,
            };

            var envelope = new Envelope { ChargingEnd = protocolEvent };
            await eventSender.SendAsync(envelope);
            _logger.LogDebug("EV {EvId} finished charging at station {StationId}", @event.EVId, @event.StationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling ChargingEnd event");
        }
    }
}
