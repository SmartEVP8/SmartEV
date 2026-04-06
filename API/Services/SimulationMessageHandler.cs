using Engine.Services;
using Protocol;

namespace API.Services;

public class SimulationMessageHandler(
    ISimulationStateService stateService,
    SimulationChannel simulationChannel,
    ILogger<SimulationMessageHandler> logger) : IEnvelopeMessageHandler
{
    private readonly ISimulationStateService _stateService = stateService;
    private readonly SimulationChannel _simulationChannel = simulationChannel;
    private readonly ILogger<SimulationMessageHandler> _logger = logger;

    public async Task<Envelope> HandleInitRequestAsync(InitRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Handling InitRequest: maxEvs={MaxEvs}, seed={Seed}",
                request.MaximumEvs, request.Seed);

            var command = new InitCommand(
                request.CostWeights.Select(cw => new SimulationCostWeight(cw.Id, cw.UpdatedValue)).ToList(),
                request.MaximumEvs,
                request.Seed,
                request.StationGeneration?.DualChargingPointProbability ?? 0.5f,
                request.StationGeneration?.TotalChargers ?? 0,
                request.ClientId);

            _simulationChannel.CommandWriter.TryWrite(command);

            var initData = new InitData();
            var response = new InitResponse
            {
                Success = true,
                InitData = initData,
                Message = "Simulation initialized",
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

            var snapshot = _stateService.GetLatestSnapshot() ?? new Protocol.SimulationSnapshot();

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

