namespace API.Services;

using Protocol;

/// <summary>
/// Manages simulation state. This is a placeholder for integration with Engine/Core.
/// TODO: Wire this to the actual simulation engine.
/// </summary>
public interface ISimulationStateService
{
    void SetInitializationData(InitData initData);
    InitData? GetInitializationData();
    void UpdateSnapshot(SimulationSnapshot snapshot);
    SimulationSnapshot? GetLatestSnapshot();
}