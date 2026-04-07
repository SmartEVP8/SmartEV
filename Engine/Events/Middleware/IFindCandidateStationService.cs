using Engine.Events;

/// <summary>
/// Service responsible for pre-computing the candidate stations for an EV and caching the results for later retrieval.
/// </summary>
public interface IFindCandidateStationService
{
    /// <summary>Gets the pre-computed candidate stations. Awaits result if it's not yet ready.</summary>
    /// <param name="evId">The EV's id.</param>
    /// <returns>The pre-computed candidate stations.</returns>
    Task<Dictionary<ushort, float>> GetCandidateStationFromCache(int evId);

    /// <summary>
    /// Computes the calculation of the path calculations from an EV's position to its relevant stations.
    /// </summary>
    /// <returns>An action that computes the candidate stations for an EV and caches the results for later retrieval.</returns>
    Action<IMiddlewareEvent> PreComputeCandidateStation();
}
