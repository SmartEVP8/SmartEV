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
    /// <inheritdoc/>
    public async void OnArrivalAtStation(ArriveAtStation @event)
    {
        try
        {
            var protocolEvent = new ArrivalEvent
            {
                StationId = @event.StationId,
                EvId = @event.EVId,
                TimestampMs = (ulong)@event.Time * 1000,
            };

            var envelope = new Envelope { Arrival = protocolEvent };
            await eventSender.SendAsync(envelope);
            logger.LogDebug("EV {EvId} arrived at station {StationId}", @event.EVId, @event.StationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling ArrivalAtStation event");
        }
    }

    /// <inheritdoc/>
    public async void OnChargingEnd(EndCharging @event)
    {
        try
        {
            var protocolEvent = new ChargingEndEvent
            {
                StationId = @event.StationId,
                EvId = @event.EVId,
                TimestampMs = (ulong)@event.Time * 1000,
            };

            var envelope = new Envelope { ChargingEnd = protocolEvent };
            await eventSender.SendAsync(envelope);
            logger.LogDebug("EV {EvId} finished charging at station {StationId}", @event.EVId, @event.StationId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling ChargingEnd event");
        }
    }
}
