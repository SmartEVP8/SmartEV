namespace API.Services;

using Engine.Events;
using Core.Charging;
using Serilog;

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
            Log.Error(ex, "Failed to send station snapshot for station {@StationId}", station.Id);
            throw;
        }
    }

    /// <inheritdoc/>
    public async void OnArrivalAtStation(ArriveAtStation @event) => SendStationSnapshot(@event.Station);

    /// <inheritdoc/>
    public async void OnChargingEnd(EndCharging @event) => SendStationSnapshot(@event.Station);
}
