namespace Engine.Events.Middleware;

using Core.Charging;
using Engine.Grid;
using Engine.Routing;
using Engine.Utils;
using Engine.Vehicles;

/// <summary>
/// Service responsible for pre-computing candidate stations for an EV and caching the results for later retrieval.
/// </summary>
public interface IFindCandidateStationService
{
    /// <summary>
    /// Gets the pre-computed candidate stations for the given EV. Awaits the result if it is not yet ready.
    /// </summary>
    /// <param name="evId">The EV's id.</param>
    /// <returns>
    /// A dictionary mapping each candidate station's id to the travel duration from the EV's current position.
    /// </returns>
    Task<Dictionary<ushort, float>> GetCandidateStationFromCache(int evId);

    /// <summary>
    /// Returns a middleware action that, when invoked with a <see cref="FindCandidateStations"/> event,
    /// kicks off an async computation of the candidate stations for the EV and caches the result for
    /// later retrieval via <see cref="GetCandidateStationFromCache"/>.
    /// </summary>
    /// <returns>
    /// An <see cref="Action{IMiddlewareEvent}"/> that accepts a <see cref="FindCandidateStations"/> event
    /// and starts the background computation.
    /// </returns>
    Action<IMiddlewareEvent> PreComputeCandidateStation();
}

