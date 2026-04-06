using Google.Protobuf;
using Google.Protobuf.Collections;
using Smartev.Api.V1;

namespace API.Services;

/// <summary>
/// Processes simulation protocol messages and generates appropriate responses.
/// </summary>
public interface IEnvelopeMessageHandler
{
    Task<Envelope> HandleInitRequestAsync(InitRequest request, CancellationToken cancellationToken);
    Task<Envelope> HandleGetSnapshotRequestAsync(GetSnapshotRequest request, CancellationToken cancellationToken);
}

public class SimulationMessageHandler : IEnvelopeMessageHandler
{
    private readonly ISimulationStateService _stateService;
    private readonly ILogger<SimulationMessageHandler> _logger;

    public SimulationMessageHandler(
        ISimulationStateService stateService,
        ILogger<SimulationMessageHandler> logger)
    {
        _stateService = stateService;
        _logger = logger;
    }

    public async Task<Envelope> HandleInitRequestAsync(InitRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Handling InitRequest: maxEvs={MaxEvs}, seed={Seed}",
                request.MaximumEvs, request.Seed);

            // TODO: Initialize simulation with the provided parameters
            // This will require integration with Engine/Core projects

            var initData = new InitData();
            // Note: RepeatedField<T> properties are read-only but populated via Add() or clone operations
            // TODO: Wire simulation initialization to populate with real data
            // initData.WeightRanges.Add(...);
            // initData.Chargers.Add(...);
            // initData.Stations.Add(...);

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

            // TODO: Get current simulation snapshot from Engine
            var snapshot = new SimulationSnapshot
            {
                TotalEvs = 0,
                TotalCharging = 0,
                SimulationTimeMs = 0,
                // StationStates is a RepeatedField - read-only, populated via Add()
            };

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

    private Envelope CreateErrorResponse(uint code, string message)
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

