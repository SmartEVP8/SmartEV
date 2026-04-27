namespace API.EngineManager;

using Core.Shared;

public record CostWeightDTO(int CostId, double Value);

public record EngineInitConfigDTO(
    int MaximumEVs,
    int Seed,
    List<CostWeightDTO> CostWeights,
    double DualChargerProbability,
    int NumberOfChargers,
    uint StartTime = Time.MillisecondsPerDay,
    uint EndTime = Time.MillisecondsPerDay * 7);
