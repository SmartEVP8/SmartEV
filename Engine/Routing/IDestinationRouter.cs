namespace Engine.Routing;

/// <summary>
/// Defines an interface for an ORSM router that can compute routes to a destination, potentially with stops in between.
/// </summary>
public interface IDestinationRouter
{
    /// <summary>
    /// Queries the route from the electric vehicle's current position to a destination, potentially with stops in between.
    /// </summary>
    /// <param name="coords">A flat array of coordinates representing the route.</param>
    /// <returns>A tuple containing the duration and polyline string for the route.</returns>
    public (float duration, string polyline) QueryDestination(double[] coords);
}
