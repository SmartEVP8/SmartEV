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
    Station? GetStation(ushort stationId);

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
}