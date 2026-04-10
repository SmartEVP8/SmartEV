namespace Engine.Vehicles;

using Core.Shared;
using Engine.DayCycles;

/// <summary>
/// This class provides the amount of cars on the road and the period in seconds for which
/// the amount of cars is calculated.
/// </summary>
/// <param name="SpawningFrequency"> The frequency to spawn cars in, in seconds.</param>
/// <param name="SpawnFraction"> A fraction of the total EVs that are supposed to be on the road, to avoid overpopulating the system.</param>
public class CarsInPeriod(Time SpawningFrequency, double SpawnFraction)
{
    private readonly double _fractionPerPeriod = SpawnFraction / (60000.0 * 60 / SpawningFrequency);

    /// <summary>
    /// Gets the estimated number of cars to spawn in the current period based on the
    /// day of the week and hour of the day.
    /// </summary>
    /// <param name="currentTime">The current time in the simulation.</param>
    /// <returns> A SpawnInstruction containing the amount of cars to spawn and the period. </returns>
    public int GetCarsInPeriod(Time currentTime)
    {
        var day = currentTime.DayOfWeek;
        var hour = currentTime.Hours;
        return (int)(CarsOnRoad.GetEVsOnRoad(day, (int)hour) * _fractionPerPeriod);
    }
}
