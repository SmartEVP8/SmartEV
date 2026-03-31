namespace Engine.Routing;

/// <summary>
/// Defines an interface for an ORSM router that can compute routes to a destination, potentially with stops in between.
/// </summary>
public interface IDestinationRouter
{
    /// <summary>
    /// Queries the route from the electric vehicle's current position to a destination, potentially with stops in between.
    /// </summary>
    /// <param name="evLon">The longitude coordinate of the electric vehicle.</param>
    /// <param name="evLat">The latitude coordinate of the electric vehicle.</param>
    /// <param name="stationLon">The longitude coordinate of the intermediate station.</param>
    /// <param name="stationLat">The latitude coordinate of the intermediate station.</param>
    /// <param name="destLon">The longitude coordinate of the destination.</param>
    /// <param name="destLat">The latitude coordinate of the destination.</param>
    /// <param name="index">A station index to query along the route.</param>
    /// <returns>A tuple containing the duration and polyline string for the route.</returns>
    public (float duration, string polyline) QueryDestinationWithStop(double evLon, double evLat, double stationLon, double stationLat, double destLon, double destLat, ushort index = 0);
}
