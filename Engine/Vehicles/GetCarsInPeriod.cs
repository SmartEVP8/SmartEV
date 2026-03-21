namespace Engine.Vehicles;

using Engine.DayCycles;
using System;

/// <summary>
/// This class provides the amount of cars on the road and the period in seconds for which 
/// the amount of cars is calculated.
/// </summary>
public class CarsInPeriod
{
    /// <summary>
    /// Gets the estimated number of cars to spawn in the current period based on the 
    /// day of the week and hour of the day.
    /// </summary>
    /// <param name="day"> The day of the week. </param>
    /// <param name="hourOfDay"> The hour of the day. </param>
    /// <param name="SpawnFraction"> A fraction of the total EVs that are supposed to be 
    ///                              on the road, to avoid overpopulating the system. </param>
    /// <param name="SpawningFrequency"> The frequency to spawn cars in, in seconds. </param>
    /// <returns> A SpawnInstruction containing the amount of cars to spawn and the period. </returns>
    public int GetCarsInPeriod(DayOfWeek day, int hourOfDay, double SpawnFraction, uint SpawningFrequency)
    {
        var periodsPerHour = 60.0 * 60.0 / SpawningFrequency;
        var fractionPerPeriod = SpawnFraction / periodsPerHour;

        var amount = (int)(CarsOnRoad.GetEVsOnRoad(day, hourOfDay) * fractionPerPeriod);

        return amount;
    }
}