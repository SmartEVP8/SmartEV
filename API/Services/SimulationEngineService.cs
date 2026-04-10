namespace API.Services;

using System.Net.WebSockets;
using Engine;
using Engine.Events;
using Engine.Services;
using Engine.Vehicles;
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
    EVStore evStore,
    ILogger<SimulationEngineService> logger) : BackgroundService, IEngineEventSubscriber
{
    private readonly SimulationChannel _simulationChannel = simulationChannel;
    private readonly EnvelopeWebSocketHandler _envelopeHandler = envelopeHandler;
    private readonly EVStore _evStore = evStore;
    private readonly ILogger<SimulationEngineService> _logger = logger;

    private readonly Lazy<Simulation> _simulation = new(() => services.GetRequiredService<Simulation>(), isThreadSafe: true);
    private readonly Lazy<StationService> _stationService = new(() => services.GetRequiredService<StationService>(), isThreadSafe: true);

    private readonly Lock _clientLock = new();

    private WebSocket? _client;

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
    /// Background service execution method. Runs the Engine and listens for commands from the SimulationChannel.
    /// </summary>
    /// <param name="stoppingToken">A token to signal cancellation of the background service.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.WhenAll(
                HandleCommandsAsync(stoppingToken));
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
            await HandleCommandAsync(command, stoppingToken);
        }
    }

    private async Task HandleCommandAsync(SimulationCommand command, CancellationToken cancellationToken = default)
    {
        if (command is InitCommand init)
        {
            _logger.LogInformation("Running simulation with seed={Seed}, maxEvs={MaxEvs}", init.Seed, init.MaximumEvs);
            await SendInitEngineDataAsync(init, cancellationToken);
            _ = _simulation.Value.Run();
        }
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

    /// <summary>
    /// Builds InitEngineData and sends it to the connected client.
    /// </summary>
    private async Task SendInitEngineDataAsync(InitCommand initCommand, CancellationToken cancellationToken = default)
    {
        try
        {
            var initData = new InitEngineData();

            foreach (var cw in initCommand.CostWeights)
            {
                initData.CostWeights.Add(new CostWeight { Id = cw.Id, UpdatedValue = cw.UpdatedValue });
            }

            foreach (var station in _stationService.Value.GetAllStations())
            {
                var stationInit = new StationInit
                {
                    Id = station.Id,
                    Address = station.Address,
                    Pos = new Position { Lat = station.Position.Latitude, Lon = station.Position.Longitude },
                };
                initData.Stations.Add(stationInit);

                foreach (var charger in station.Chargers)
                {
                    var chargerProto = new Charger
                    {
                        Id = charger.Id,
                        MaxPowerKw = charger.MaxPowerKW,
                        StationId = station.Id,
                        IsDual = charger.GetType().Name == "DualCharger",
                    };
                    initData.Chargers.Add(chargerProto);
                }
            }

            var envelope = new Envelope { InitEngineData = initData };
            await SendToClientAsync(envelope, cancellationToken);

            _logger.LogInformation(
                "Sent InitEngineData with {StationCount} stations and {ChargerCount} chargers",
                initData.Stations.Count,
                initData.Chargers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending InitEngineData to client");
        }
    }

    /// <summary>
    /// Handles the GetStationSnapshot client request and sends the current station state back to the client.
    /// </summary>
    /// <param name="request">The GetStationSnapshot request containing the station ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task OnGetStationSnapshot(GetStationSnapshot request, CancellationToken cancellationToken = default)
    {
        try
        {
            var stationId = request.StationId;
            var stationState = new StationState { StationId = stationId };
            var station = _stationService.Value.GetStation((ushort)stationId);
            if (station == null)
            {
                _logger.LogWarning("Station with ID {StationId} not found", stationId);
                return;
            }

            foreach (var charger in station.Chargers)
            {
                var chargerState = CreateChargerState(charger.Id);
                stationState.ChargerStates.Add(chargerState);
            }

            stationState.EvsOnRoute.AddRange(GetEVsOnRoute((ushort)stationId));

            var envelope = new Envelope { StationStateResponse = stationState };
            await SendToClientAsync(envelope, cancellationToken);

            _logger.LogDebug(
                "Sent StationState for station {StationId}: {ChargerCount} chargers, {QueueTotal} total queued EVs, {EvsOnRoute} EVs on route",
                stationId,
                stationState.ChargerStates.Count,
                stationState.ChargerStates.Sum(c => c.QueueSize),
                stationState.EvsOnRoute.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling GetStationSnapshot request for station {StationId}", request.StationId);
        }
    }

    private Protocol.ChargerState CreateChargerState(int chargerId)
    {
        // Remove when utilization is implemented
        var random = new Random(chargerId);
        var tempUtilization = random.NextSingle();

        var engineChargerState = _stationService.Value.GetChargerState(chargerId);
        if (engineChargerState is null)
            return null!;

        var chargerState = new Protocol.ChargerState
        {
            IsActive = !engineChargerState.IsFree,

            // TODO: Utilization
            Utilization = tempUtilization,

            ChargerId = (uint)chargerId,
            QueueSize = (uint)engineChargerState.Queue.Count,
        };

        if (engineChargerState.SessionA is not null)
            chargerState.EvsInQueue.Add(CreateEVChargerState(engineChargerState.SessionA));

        if (engineChargerState.SessionB is not null)
            chargerState.EvsInQueue.Add(CreateEVChargerState(engineChargerState.SessionB));

        foreach (var (evId, ev) in engineChargerState.Queue)
        {
            chargerState.EvsInQueue.Add(new EVChargerState
            {
                EvId = evId,
                Soc = (float)ev.CurrentSoC,
                TargetSoc = (float)ev.TargetSoC,
            });
        }

        return chargerState;
    }

    private static EVChargerState CreateEVChargerState(ActiveSession session) =>
        new()
        {
            EvId = session.EVId,
            Soc = (float)session.EV.CurrentSoC,
            TargetSoc = (float)session.EV.TargetSoC,
        };

    private EVOnRoute[] GetEVsOnRoute(ushort stationId)
    {
        var evsOnRoute = _stationService.Value.GetEVsOnRouteToStation(stationId);

        var result = new List<EVOnRoute>();
        foreach (var evId in evsOnRoute)
        {
            var ev = _evStore.Get(evId);
            var evOnRoute = new EVOnRoute
            {
                EvId = evId,
            };

            foreach (var waypoint in ev.Journey.Current.Waypoints)
            {
                evOnRoute.Waypoints.Add(new Position
                {
                    Lat = waypoint.Latitude,
                    Lon = waypoint.Longitude,
                });
            }

            result.Add(evOnRoute);
        }

        return [.. result];
    }
}
