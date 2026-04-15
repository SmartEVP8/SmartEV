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
}
