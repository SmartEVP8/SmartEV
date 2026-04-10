namespace API.Services;

using Engine.Services;
using Protocol;

/// <summary>
/// Processes incoming protocol messages and translates them to Engine commands.
/// </summary>
/// <param name="simulationChannel">The simulation channel.</param>
/// <param name="logger">The logger.</param>
public class SimulationMessageHandler(
    SimulationChannel simulationChannel,
    ILogger<SimulationMessageHandler> logger)
{
    private readonly SimulationChannel _simulationChannel = simulationChannel;
    private readonly ILogger<SimulationMessageHandler> _logger = logger;

    /// <summary>
    /// Handles InitRequest by queuing the simulation command.
    /// The engine will send InitEngineData back as an event.
    /// </summary>
    /// <param name="request">The init request.</param>
    public void HandleInitRequest(InitRequest request)
    {
        try
        {
            _logger.LogDebug(
                "Handling InitRequest: seed={Seed}, maxEvs={MaxEvs}",
                request.Seed,
                request.MaximumEvs);

            var command = new InitCommand(
                request.CostWeights.Select(cw => new SimulationCostWeight(cw.Id, cw.UpdatedValue)).ToList(),
                request.MaximumEvs,
                request.Seed,
                request.StationGeneration?.DualChargingPointProbability ?? 0.5f,
                request.StationGeneration?.TotalChargers ?? 0);

            _simulationChannel.CommandWriter.TryWrite(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling InitRequest");
        }
    }
}
