namespace API.Services;

using Engine.Services;
using Protocol;

/// <summary>
/// Processes incoming protocol messages and translates them to Engine commands or state queries.
/// </summary>
/// <param name="stateService">The simulation state service.</param>
/// <param name="simulationChannel">The simulation channel.</param>
/// <param name="logger">The logger.</param>
public class SimulationMessageHandler(
    SimulationStateService stateService,
    SimulationChannel simulationChannel,
    ILogger<SimulationMessageHandler> logger)
{
    private readonly SimulationStateService _stateService = stateService;
    private readonly SimulationChannel _simulationChannel = simulationChannel;
    private readonly ILogger<SimulationMessageHandler> _logger = logger;

    public async Task<Envelope> HandleInitRequestAsync(InitRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Handling InitRequest from client {ClientId}: maxEvs={MaxEvs}, seed={Seed}",
                request.ClientId, request.MaximumEvs, request.Seed);

            var command = new InitCommand(
                request.CostWeights.Select(cw => new SimulationCostWeight(cw.Id, cw.UpdatedValue)).ToList(),
                request.MaximumEvs,
                request.Seed,
                request.StationGeneration?.DualChargingPointProbability ?? 0.5f,
                request.StationGeneration?.TotalChargers ?? 0,
                request.ClientId);

            // Send restart command to Engine
            _simulationChannel.CommandWriter.TryWrite(command);

            // Return success
            var response = new InitResponse
            {
                Success = true,
                Message = "Simulation restart initiated",
            };

            return new Envelope
            {
                InitResponse = response,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling InitRequest");
            return CreateErrorResponse(500, ex.Message);
        }
    }

    public async Task<Envelope> HandleGetSnapshotRequestAsync(GetSnapshotRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Handling GetSnapshotRequest");

            var snapshot = _stateService.GetLatestSnapshot() ?? new SimulationSnapshot();

            return new Envelope
            {
                SnapshotResponse = snapshot,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling GetSnapshotRequest");
            return CreateErrorResponse(500, ex.Message);
        }
    }

    private static Envelope CreateErrorResponse(uint code, string message)
    {
        return new Envelope
        {
            Error = new ErrorResponse
            {
                Code = code,
                Message = message,
            },
        };
    }
}
