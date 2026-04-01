namespace Engine.Cost;

using Core.Charging;
using Core.Shared;
using Core.Vehicles;

/// <summary>
/// Computes the cost of detouring to each station and selects the station with the lowest cost.
/// </summary>
public interface IComputeCost
{
    /// <summary>Computes the cost of detouring to each station and selects the station with the lowest cost.</summary>
    /// <param name="ev">The EV for which to compute costs.</param>
    /// <param name="stationDurations">A map of station ID to travel duration for each station.</param>
    /// <param name="time">The current time.</param>
    /// <returns>The station with the lowest cost.</returns>
    public Station Compute(ref EV ev, Dictionary<ushort, float> stationDurations, Time time);
}

