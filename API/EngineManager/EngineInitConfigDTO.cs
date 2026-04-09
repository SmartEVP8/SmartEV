namespace API.EngineManager;

public record CostWeightDTO(int CostId, double Value);
public record EngineInitConfigDTO(
        int MaximumEVs,
        int Seed,
        List<CostWeightDTO> CostWeights,
        double DualChargerProbability,
        int NumberOfChargers);
