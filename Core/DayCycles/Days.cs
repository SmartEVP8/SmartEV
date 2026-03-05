namespace Core.DayCycles;

using static Cars;

/// <summary>
/// This class provides congestion values for each hour of each day of the week.
/// </summary>
public static class PassingOfDay
{
    /// <summary>
    /// Maximum measured congestion (km with critical congestion) from the dataset.
    /// Used to normalize the congestion values.
    /// </summary>
    private const float MaxCongestionKm = 125f;

    /// <summary>
    /// An enumeration representing the days of the week.
    /// </summary>
    public enum Day
    {
        Monday,
        Tuesday,
        Wednesday,
        Thursday,
        Friday,
        Saturday,
        Sunday,
    }

    /// <summary>
    /// Raw congestion data representing km of critically congested road.
    /// Numbers are estimated from the Vejdirektoratet dataset, based on eye estimations.
    /// Source: https://www.vejdirektoratet.dk/side/trafikkens-udvikling-i-tal
    /// </summary>
    private static readonly float[,] CongestionKm =
    {
        // Monday
        { 5, 5, 5, 5, 20, 40, 110, 120, 90, 60, 40, 30, 40, 50, 60, 65, 60, 35, 40, 35, 35, 20, 10, 5 },

        // Tuesday
        { 5, 5, 5, 5, 20, 40, 120, 125, 90, 60, 40, 35, 50, 60, 80, 80, 60, 40, 35, 30, 20, 15, 10, 5 },

        // Wednesday
        { 5, 5, 5, 5, 20, 30, 60, 70, 60, 50, 20, 30, 40, 50, 60, 60, 50, 30, 35, 30, 20, 15, 10, 5 },

        // Thursday
        { 5, 5, 5, 5, 20, 30, 60, 70, 60, 50, 30, 40, 60, 80, 80, 70, 60, 50, 40, 30, 20, 20, 10, 5 },

        // Friday
        { 5, 5, 5, 5, 10, 15, 20, 30, 40, 30, 25, 40, 55, 75, 72, 65, 55, 40, 30, 20, 20, 15, 10, 5 },

        // Saturday
        { 5, 5, 5, 5, 5, 5, 8, 10, 15, 18, 20, 30, 40, 40, 30, 25, 28, 25, 26, 25, 20, 15, 10, 5 },

        // Sunday
        { 5, 5, 5, 5, 5, 5, 8, 10, 12, 15, 18, 20, 25, 20, 25, 30, 32, 32, 28, 26, 20, 15, 8, 5 },
    };

    /// <summary>
    /// Gets the estimated number of EVs on the road for a specific day and hour.
    /// </summary>
    /// <param name="day">The day of the week.</param>
    /// <param name="hour">The hour of the day (0-23).</param
    /// <returns>The estimated number of EVs on the road.</returns>
    public static int GetEVsOnRoad(Day day, int hour)
    {
        if (hour < 0 || hour > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23.");
        }

        if ((int)day < 0 || (int)day > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(day), "Day must be between Monday and Sunday.");
        }

        float km = CongestionKm[(int)day, hour];

        float congestionIndex = km / MaxCongestionKm;

        float cars = BaselineCars + ((PeakCars - BaselineCars) * congestionIndex);

        return (int)Math.Min(cars, TotalEVs);
    }
}