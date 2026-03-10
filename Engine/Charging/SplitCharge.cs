namespace Engine.Charging;

/// <summary>
/// PowerDistributor provides a method to fairly distribute available power between two consumers,
/// ensuring that each consumer receives as much power as possible up to their capacity,
/// while also calculating any wasted power.
/// </summary>
public static class PowerDistributor
{
    /// <summary>
    /// Distributes available power between two consumers using a fair-split algorithm.
    /// Each consumer receives up to its capacity, with any remainder offered back to the other.
    /// </summary>
    /// <param name="available">Total power available to distribute.</param>
    /// <param name="capacity1">Maximum power consumer 1 can take.</param>
    /// <param name="capacity2">Maximum power consumer 2 can take.</param>
    /// <returns>A tuple of (allocated1, allocated2, wasted).</returns>
    public static (double Allocated1, double Allocated2, double Wasted) Distribute(
        double available, double capacity1, double capacity2)
    {
        var consumer1 = Math.Min(available / 2, capacity1);
        var consumer2 = Math.Min(available - consumer1, capacity2);
        consumer1 = Math.Min(available - consumer2, capacity1);
        var leftoverPower = available - consumer1 - consumer2;

        return (consumer1, consumer2, leftoverPower);
    }
}