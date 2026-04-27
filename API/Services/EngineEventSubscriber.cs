namespace API.Services;

using Engine.Events;
using Core.Helper;
using Core.Charging;

/// <summary>
/// Bridges engine events to protocol events and sends them to the connected client.
/// </summary>
public sealed class EngineEventSubscriber(
    SnapshotHandler snapshotHandler,
    IEventSender eventSender) : IEngineEventSubscriber
{
    private async void SendStationSnapshot(Station station)
    {
        try
        {
            var envelope = snapshotHandler.BuildStationSnapshot(station);
            await eventSender.SendAsync(envelope);
        }
        catch (Exception ex)
        {
            Log.Error(0, 0, ex, ("StationId", station));
        }
    }

    /// <inheritdoc/>
    public async void OnArrivalAtStation(ArriveAtStation @event) => SendStationSnapshot(@event.Station);

    /// <inheritdoc/>
    public async void OnChargingEnd(EndCharging @event) => SendStationSnapshot(@event.Station);
}
