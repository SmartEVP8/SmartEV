namespace API.Services;

using Engine.Events;
using Engine.Protocol;
using Engine.Services;

/// <summary>
/// Hosts Engine as a BackgroundService. Bridges Engine events to protocol events via SimulationChannel.
/// Reads commands, translates Engine events to protocol events.
/// </summary>
public sealed class SimulationEngineService(
    Simulation simulation,
    SimulationChannel simulationChannel,
    StationService stationService,
    ILogger<SimulationEngineService> logger) : BackgroundService, IEngineEventSubscriber
{
    private readonly Simulation _simulation = simulation;
    private readonly SimulationChannel _simulationChannel = simulationChannel;
    private readonly StationService _stationService = stationService;
    private readonly ILogger<SimulationEngineService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var command in _simulationChannel.CommandReader.ReadAllAsync(stoppingToken))
        {
            HandleCommand(command);
        }
    }

    private void HandleCommand(SimulationCommand command)
    {
        if (command is InitCommand init)
        {
            _logger.LogInformation("Running simulation with seed={Seed}, maxEvs={MaxEvs}", init.Seed, init.MaximumEvs);
            _ = _simulation.Run();
        }
    }

    public void OnArrivalAtStation(ArriveAtStation @event)
    {
        var protocolEvent = new ArrivalEvent(
            StationId: @event.StationId,
            EvId: @event.EVId,
            TimestampMs: (ulong)@event.Time.T * 1000);
        _simulationChannel.EventWriter.TryWrite(protocolEvent);
    }

    public void OnChargingEnd(EndCharging @event)
    {
        var chargerState = _stationService.GetChargerState(@event.ChargerId);
        if (chargerState is not null)
        {
            var protocolEvent = new ChargingEndEvent(
                StationId: chargerState.StationId,
                EvId: @event.EVId,
                TimestampMs: (ulong)@event.Time.T * 1000);
            _simulationChannel.EventWriter.TryWrite(protocolEvent);
        }
    }
}
