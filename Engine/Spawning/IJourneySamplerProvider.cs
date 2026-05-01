namespace Engine.Spawning;

using Core.Shared;
using Engine.Grid;

/// <summary>
/// A shared store of the currently computed samplers.
/// </summary>
public interface IJourneySamplerProvider
{
    /// <summary>
    /// Sets the current journey sampler to the sampler mathing that hour.
    /// </summary>
    /// <param name="time">The current simulation time used to determine which sampler to return.</param>
    void SetCurrent(Time time);

    /// <summary>
    /// Gets the currently active journey sampler, which should match the current simulation time.
    /// </summary>
    IJourneySampler Current { get; }
}
