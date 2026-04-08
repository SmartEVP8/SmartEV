namespace Engine.Services;

public abstract record SimulationCommand;

public sealed record InitCommand(
    List<SimulationCostWeight> CostWeights,
    uint MaximumEvs,
    int Seed,
    float DualChargingPointProbability,
    int TotalChargers) : SimulationCommand;

public sealed record SimulationCostWeight(int Id, float UpdatedValue);
