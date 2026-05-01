namespace Engine.Spawning;

using Core.Shared;
using Engine.Grid;

/// <summary>
/// A shared store of the currently computed samplers.
/// </summary>
public interface IJourneySamplerProvider
{
    /// <summary>
    /// Gets the current journey sampler based on the provided simulation time.
    /// </summary>
    /// <param name="time">The current simulation time used to determine which sampler to return.</param>
    /// <returns>The journey sampler corresponding to the current simulation time.</returns>
    IJourneySampler SetCurrent(Time time);

    IJourneySampler Current { get; }
}
