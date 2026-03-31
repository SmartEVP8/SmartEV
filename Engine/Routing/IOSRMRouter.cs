namespace Engine.Routing;

using Core.Charging;

/// <summary>
/// Interface for a router that uses the OSRM API to calculate routes and durations.
/// </summary>
public interface IOSRMRouter : IMatrixRouter, IDisposable, IPointToPointRouter, IDestinationRouter
{
    /// <summary>
    /// Initializes the router with the given stations.
    /// </summary>
    /// <param name="stations">The list of stations to initialize the router with.</param>
    void InitStations(List<Station> stations);

    /// <summary>
    /// Queries the durations and distances from an electric vehicle to specified stations.
    /// </summary>
    /// <param name="evLon">The longitude coordinate of the electric vehicle.</param>
    /// <param name="evLat">The latitude coordinate of the electric vehicle.</param>
    /// <param name="destLon">The longitude coordinate of the destination.</param>
    /// <param name="destLat">The latitude coordinate of the destination.</param>
    /// <param name="indices">An array of station indices to query.</param>
    /// <returns>A tuple containing arrays of durations and distances to each station.</returns>
    (float[] durations, float[] distances) QueryStationsWithDest(
        double evLon,
        double evLat,
        double destLon,
        double destLat,
        ushort[] indices);

    /// <summary>
    /// Queries the durations and distances from an electric vehicle to specified stations.
    /// </summary>
    /// <param name="evLon">The longitude coordinate of the electric vehicle.</param>
    /// <param name="evLat">The latitude coordinate of the electric vehicle.</param>
    /// <param name="indices">An array of station indices to query.</param>
    /// <returns>A tuple containing arrays of durations and distances to each station.</returns>
    (float[] durations, float[] distances) QueryStations(
        double evLon,
        double evLat,
        ushort[] indices);
}
