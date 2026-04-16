namespace API.Services;

using Engine.Events;

/// <summary>
/// Bridges engine events to protocol events and sends them to the connected client.
/// </summary>
public sealed class EngineEventSubscriber(
    SnapshotHandler snapshotHandler,
    IEventSender eventSender,
    ILogger<EngineEventSubscriber> logger) : IEngineEventSubscriber
{
    private async void SendStationSnapshot(ushort stationId)
    {
        try
        {
            var envelope = snapshotHandler.BuildStationSnapshot(stationId);
            await eventSender.SendAsync(envelope);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending station snapshot for station ID {StationId}", stationId);
        }
    }

    /// <inheritdoc/>
    public async void OnArrivalAtStation(ArriveAtStation @event) => SendStationSnapshot(@event.StationId);

    /// <inheritdoc/>
    public async void OnChargingEnd(EndCharging @event) => SendStationSnapshot(@event.StationId);
}
