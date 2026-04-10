namespace API.Services;

using System.Net.WebSockets;
using Engine;
using Engine.Events;
using Engine.Services;
using Microsoft.Extensions.DependencyInjection;
using Protocol;

/// <summary>
/// Hosts Engine as a BackgroundService. Bridges Engine events to protocol events via SimulationChannel.
/// Reads commands and translates Engine events to protocol events for client streaming.
/// </summary>
public sealed class SimulationEngineService(
    IServiceProvider services,
    SimulationChannel simulationChannel,
    EnvelopeWebSocketHandler envelopeHandler,
    ILogger<SimulationEngineService> logger) : IEngineEventSubscriber, IDisposable
{
    private readonly IServiceProvider _services = services;
    private readonly SimulationChannel _simulationChannel = simulationChannel;
    private readonly EnvelopeWebSocketHandler _envelopeHandler = envelopeHandler;
    private readonly ILogger<SimulationEngineService> _logger = logger;

    private readonly Lazy<Simulation> _simulation = new(() => services.GetRequiredService<Simulation>(), isThreadSafe: true);
    private readonly Lazy<StationService> _stationService = new(() => services.GetRequiredService<StationService>(), isThreadSafe: true);

    private readonly Lock _clientLock = new();

    private WebSocket? _client;

    private Task? _simulationTask;
    private CancellationTokenSource? _cts;
    public bool IsRunning => _simulationTask != null && !_simulationTask.IsCompleted;

    /// <summary>
    /// Attaches a WebSocket client for event broadcasting. Only one client is supported at a time.
    /// </summary>
    /// <param name="socket">The WebSocket client to attach.</param>
    public void AttachClient(WebSocket socket)
    {
        lock (_clientLock)
        {
            _client = socket;
        }
    }

    /// <summary>
    /// Detaches the current WebSocket client, if any. Should be called when the client disconnects.
    /// </summary>
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

    /// <summary>
    /// Starts the simulation engine loop manually. Only starts if not already running.
    /// </summary>
    public async Task StartEngineAsync()
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();
        _simulationTask = Task.Run(() => RunSimulationLoop(_cts.Token));
        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the simulation engine loop if running.
    /// </summary>
    public async Task StopEngineAsync()
    {
        if (!IsRunning) return;
        _cts?.Cancel();
        if (_simulationTask != null)
            await _simulationTask;
    }

    private async Task RunSimulationLoop(CancellationToken stoppingToken)
    {
        try
        {
            await _simulation.Value.Run();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SimulationEngineService stopped");
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    /// <summary>
    /// Handles the ArriveAtStation event from the Engine and translates it to an ArrivalEvent for the protocol, then sends it to the client.
    /// </summary>
    /// <param name="event">The ArriveAtStation event from the Engine.</param>
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

    /// <summary>
    /// Handles the EndCharging event from the Engine and translates it to a ChargingEndEvent for the protocol, then sends it to the client.
    /// </summary>
    /// <param name="event">The EndCharging event from the Engine.</param>
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
