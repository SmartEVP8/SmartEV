namespace Engine.Services.StationServiceHelpers;

using Core.Charging.ChargingModel;
using Core.Shared;

/// <summary>
/// Handles the session lifecycle for a charger, abstracting over single and dual charger behaviour.
/// </summary>
public interface IChargerHandler
{
    /// <summary>
    /// Attempts to dequeue and start charging the next EV(s) in the queue.
    /// </summary>
    /// <param name="simNow">The current simulation time.</param>
    /// <param name="stationId">The station id.</param>
    void StartNext(Time simNow, ushort stationId);

    /// <summary>
    /// Ends the session for the given EV and disconnects it from the charger.
    /// </summary>
    /// <param name="evId">The id of the EV whose session is ending.</param>
    /// <param name="simNow">The current simulation time.</param>
    /// <returns>The final SoC of the EV, or null if no matching session was found.</returns>
    double? EndSession(int evId, Time simNow);

    /// <summary>
    /// Estimates the next time the charger is available and generates a schedule of EV finish times.
    /// <paramref name="evsOverride"/> Defaults to its currently charging evs + queue.
    /// </summary>
    /// <param name="simNow">The current time.</param>
    /// <param name="evsOverride">Overrides the estimate for a custom list of connected evs.</param>
    /// <returns>A tuple containing the next time the charger is available and the chronological schedule.</returns>
    (Time AvailableAt, IReadOnlyList<(int EVId, Time FinishTime)> Schedule) EstimateWaitTime(Time simNow, IReadOnlyList<ConnectedEV>? evsOverride = null);
}
