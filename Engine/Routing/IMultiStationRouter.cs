namespace Engine.Routing;

/// <summary>
/// Queries durations and distances from an electric vehicle to specified stations, considering a destination point.
/// </summary>
public interface IMultiStationRouter
{
    /// <summary>
    /// Queries the durations and distances from an electric vehicle to specified stations.
    /// </summary>
    /// <param name="evLon">The longitude coordinate of the electric vehicle.</param>
    /// <param name="evLat">The latitude coordinate of the electric vehicle.</param>
    /// <param name="destLon">The longitude coordinate of the destination.</param>
    /// <param name="destLat">The latitude coordinate of the destination.</param>
    /// <param name="indices">An array of station indices to query.</param>
    /// <returns>A tuple containing arrays of durations and distances to each station.</returns>
    public RoutingLegsResult QueryStationsWithDest(
        double evLon,
        double evLat,
        double destLon,
        double destLat,
        ushort[] indices);
}
