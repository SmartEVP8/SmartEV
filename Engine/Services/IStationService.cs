namespace Engine.Services;

using Core.Charging;

/// <summary>
/// Provides access to station data.
/// </summary>
public interface IStationService
{
    /// <summary>
    /// Gets the station with the given ID.
    /// </summary>
    /// <param name="stationId">The station ID.</param>
    /// <returns>The station.</returns>
    Station GetStation(ushort stationId);

    /// <summary>
    /// Gets the total queue size across all chargers at the station with the given ID.
    /// </summary>
    /// <param name="stationId">The station ID.</param>
    /// <returns>The total queue size.</returns>
    int GetTotalQueueSize(ushort stationId);

    /// <summary>
    /// Gets all EVs currently on route to the given station.
    /// </summary>
    /// <param name="stationId">The station ID.</param>
    /// <returns>A collection of EV IDs on route to the station.</returns>
    IEnumerable<int> GetEVsOnRouteToStation(ushort stationId);

    /// <summary>
    /// Adds an EV to the set of EVs on route to a station.
    /// </summary>
    /// <param name="evId">The ID of the EV to add.</param>
    /// <param name="stationId">The ID of the station to which the EV is on route.</param>
    public void AddEVOnRoute(int evId, ushort stationId);
}