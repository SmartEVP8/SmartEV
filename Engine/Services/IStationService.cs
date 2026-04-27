namespace Engine.Services;

using Core.Charging;
using Core.Shared;

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
    /// Calculates the estimated availability time for an EV arriving at a station, accounting for active sessions, physical queues, and future reservations.
    /// </summary>
    /// <param name="stationId">The unique identifier of the target station.</param>
    /// <param name="simNow">The current simulation time, used as the baseline for evaluating active and physically queued sessions.</param>
    /// <param name="arrival">The projected arrival time of the EV used to filter relevant prior reservations.</param>
    /// <returns>The expected absolute time a charger will become available for the arriving EV. Returns simNow if a charger is immediately available.</returns>
    public Time ExpectedWaitTime(ushort stationId, Time simNow, Time arrival);
}
