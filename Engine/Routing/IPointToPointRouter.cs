namespace Engine.Routing;

/// <summary>
/// Defines the interface for a point-to-point router, which provides functionality to query the duration and polyline for a route between two points.
/// </summary>
public interface IPointToPointRouter
{
    /// <summary>
    /// Queries the duration and polyline for a route between the given source and destination coordinates.
    /// </summary>
    /// <param name="evLon">The longitude of the source position.</param>
    /// <param name="evLat">The latitude of the source position.</param>
    /// <param name="destLon">The longitude of the destination position.</param>
    /// <param name="destLat">The latitude of the destination position.</param>
    /// <returns>A tuple containing the duration and polyline for the route.</returns>
    (float duration, string polyline) QuerySingleDestination(double evLon, double evLat, double destLon, double destLat);
}
