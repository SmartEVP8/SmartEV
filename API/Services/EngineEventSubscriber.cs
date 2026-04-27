namespace API.Services;

using Engine.Events;
using Core.Helper;

/// <summary>
/// Bridges engine events to protocol events and sends them to the connected client.
/// </summary>
public sealed class EngineEventSubscriber(
    SnapshotHandler snapshotHandler,
    IEventSender eventSender) : IEngineEventSubscriber
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
            Log.Error(0, 0, ex, ("StationId", stationId));
        }
    }

    /// <inheritdoc/>
    public async void OnArrivalAtStation(ArriveAtStation @event) => SendStationSnapshot(@event.StationId);

    /// <inheritdoc/>
    public async void OnChargingEnd(EndCharging @event) => SendStationSnapshot(@event.StationId);
}
