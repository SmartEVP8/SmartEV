namespace API.Services;

using System.Net.WebSockets;
using Engine;
using Engine.Events;
using Engine.Services;
using Microsoft.Extensions.DependencyInjection;
using Protocol;

/// <summary>
/// Hosts Engine as a BackgroundService. Bridges Engine events to protocol events via SimulationChannel.
/// Reads commands, translates Engine events to protocol events, and feeds snapshots to state service.
/// </summary>
public sealed class SimulationEngineService(
    IServiceProvider services,
    SimulationChannel simulationChannel,
    SimulationStateService stateService,
    EnvelopeWebSocketHandler envelopeHandler,
    ILogger<SimulationEngineService> logger) : BackgroundService, IEngineEventSubscriber
{
    private readonly IServiceProvider _services = services;
    private readonly SimulationChannel _simulationChannel = simulationChannel;
    private readonly SimulationStateService _stateService = stateService;
    private readonly EnvelopeWebSocketHandler _envelopeHandler = envelopeHandler;
    private readonly ILogger<SimulationEngineService> _logger = logger;

    private readonly Lazy<Simulation> _simulation = new(() => services.GetRequiredService<Simulation>(), isThreadSafe: true);
    private readonly Lazy<StationService> _stationService = new(() => services.GetRequiredService<StationService>(), isThreadSafe: true);

    private WebSocket? _client;
    private readonly object _clientLock = new();

    public void AttachClient(WebSocket socket)
    {
        lock (_clientLock)
        {
            _client = socket;
        }
    }

    public void DetachClient()
    {
        lock (_clientLock)
        {
            _client = null;
        }
    }

    private WebSocket? GetClient()
    {
        lock (_clientLock)
        {
            return _client?.State == WebSocketState.Open ? _client : null;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Run command handler and snapshot reader concurrently
            await Task.WhenAll(
                HandleCommandsAsync(stoppingToken),
                HandleSnapshotsAsync(stoppingToken)
            );
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SimulationEngineService stopped");
        }
    }

    private async Task HandleCommandsAsync(CancellationToken stoppingToken)
    {
        await foreach (var command in _simulationChannel.CommandReader.ReadAllAsync(stoppingToken))
        {
            HandleCommand(command);
        }
    }

    private async Task HandleSnapshotsAsync(CancellationToken stoppingToken)
    {
        await foreach (var snapshot in _simulationChannel.SnapshotReader.ReadAllAsync(stoppingToken))
        {
            // Convert from Engine.Protocol.SimulationSnapshot to Protocol.SimulationSnapshot
            var protoSnapshot = new SimulationSnapshot
            {
                TotalEvs = snapshot.TotalEvs,
                TotalCharging = snapshot.TotalCharging,
                SimulationTimeMs = snapshot.SimulationTimeMs,
            };

            // TODO: Map StationStates when available
            _stateService.UpdateSnapshot(protoSnapshot);
        }
    }

    private void HandleCommand(SimulationCommand command)
    {
        if (command is InitCommand init)
        {
            _logger.LogInformation("Running simulation with seed={Seed}, maxEvs={MaxEvs}", init.Seed, init.MaximumEvs);
            _ = _simulation.Value.Run();
        }
    }

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
            await SendToClientAsync(envelope);
            _logger.LogDebug("EV {EvId} arrived at station {StationId}", @event.EVId, @event.StationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling ArrivalAtStation event");
        }
    }

    public async void OnChargingEnd(EndCharging @event)
    {
        try
        {
            var chargerState = _stationService.Value.GetChargerState(@event.ChargerId);
            if (chargerState is null)
            {
                return;
            }

            var protocolEvent = new ChargingEndEvent
            {
                StationId = chargerState.StationId,
                EvId = @event.EVId,
                TimestampMs = (ulong)@event.Time.T * 1000,
            };

            var envelope = new Envelope { ChargingEnd = protocolEvent };
            await SendToClientAsync(envelope);
            _logger.LogDebug("EV {EvId} finished charging at station {StationId}", @event.EVId, chargerState.StationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling ChargingEnd event");
        }
    }

    private async Task SendToClientAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        var client = GetClient();
        if (client == null)
        {
            return;
        }

        try
        {
            await _envelopeHandler.SendAsync(envelope, client, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending envelope to client");
            DetachClient();
        }
    }
}
