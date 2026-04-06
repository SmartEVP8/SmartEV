using API.Models;

namespace API.Services;

/// <summary>
/// Manages the current state of the simulation
/// Receives updates from the Headless engine and provides state queries
/// </summary>
public interface ISimulationStateService
{
    /// <summary>
    /// Update the initialization data (called when simulation starts)
    /// </summary>
    void SetInitializationData(Init initData);

    /// <summary>
    /// Get the current initialization data
    /// </summary>
    Init? GetInitializationData();

    /// <summary>
    /// Update the current station state
    /// </summary>
    void UpdateStationState(RequestStationState stationState);

    /// <summary>
    /// Get the current station state
    /// </summary>
    RequestStationState? GetStationState();

    /// <summary>
    /// Add a state snapshot (for tracking state changes over time)
    /// </summary>
    void AddStateSnapshot(StateSnapShot snapshot);

    /// <summary>
    /// Get the latest state snapshot
    /// </summary>
    StateSnapShot? GetLatestSnapshot();

    /// <summary>
    /// Record an arrival event
    /// </summary>
    void RecordArrival(ArriveAtStation arrival);

    /// <summary>
    /// Record a charging end event
    /// </summary>
    void RecordChargingEnd(EndCharging charging);

    /// <summary>
    /// Get all recorded events (for debugging/logging)
    /// </summary>
    (List<ArriveAtStation> Arrivals, List<EndCharging> ChargingEnds) GetEvents();

    /// <summary>
    /// Clear all state (for reset/new simulation)
    /// </summary>
    void Clear();
}
