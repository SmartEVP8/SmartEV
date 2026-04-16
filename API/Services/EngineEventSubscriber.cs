namespace API.Services;

using Engine.Events;
using Protocol;

/// <summary>
/// Bridges engine events to protocol events and sends them to the connected client.
/// </summary>
public sealed class EngineEventSubscriber(
    SnapshotHandler snapshotHandler,
    IEventSender eventSender,
    ILogger<EngineEventSubscriber> logger) : IEngineEventSubscriber
{
    /// <inheritdoc/>
    public async void OnArrivalAtStation(ArriveAtStation @event)
    {
        try
        {
            var envelope = snapshotHandler.BuildStationSnapshot(@event.StationId);
            await eventSender.SendAsync(envelope);
        }
        catch (Exception ex)
        {
            Log.Error(0, 0, ex);
        }
    }

    /// <inheritdoc/>
    public async void OnChargingEnd(EndCharging @event)
    {
        try
        {
            var envelope = snapshotHandler.BuildStationSnapshot(@event.StationId);
            await eventSender.SendAsync(envelope);
        }
        catch (Exception ex)
        {
            Log.Error(0, 0, ex);
        }
    }
}
