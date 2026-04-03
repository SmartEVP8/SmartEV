namespace Engine.Spawning;

using Engine.Grid;

/// <summary>
/// A shared store of the currently computed samplers.
/// </summary>
public interface IJourneySamplerProvider
{
    /// <summary>
    /// Gets the current samplers.
    /// </summary>
    IJourneySampler Current { get; }

    /// <summary>
    /// Recomputes the samplers and sets current to it.
    /// </summary>
    /// <param name="distanceScalar">Influence of distance on the gravity weight.
    /// A higher scalar increases the weight of closer cities, while a lower scalar reduces it.
    /// </param>
    /// <param name="populationScalar">Influence of city population on the gravity weight.
    /// A higher scalar increases the weight of larger cities, while a lower scalar reduces it.
    /// </param>
    /// <returns>The computed samplers. Equivelant to calling Current after Recomputation.</returns>
    IJourneySampler Recompute(float distanceScalar, float populationScalar);
}
