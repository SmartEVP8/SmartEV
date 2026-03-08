namespace Core.DayCycles;

using static Days;

/// <summary>
/// This class provides congestion values for each hour of each day of the week.
/// </summary>
public static class CarsOnRoad
{
    /// <summary>
    /// Total registered EVs in Denmark according to DST - January 2026 (Rounded down).
    /// Source: https://www.dst.dk/da/Statistik/udgivelser/NytHtml?cid=51885.
    /// </summary>
    public const int TotalEVs = 556000;

    /// <summary>
    /// Minimum number of EVs expected on the road even with almost no congestion.
    /// Was decided to be ~3% of total EVs, as we assume that there will at least be some
    /// EVs on the road at all times. (556,000 * 0.03 = 16,680).
    /// </summary>
    public const int BaselineCars = TotalEVs * 3 / 100;

    /// <summary>
    /// Maximum number of EVs on the road during peak congestion.
    /// Estimated as ~75% of total EVs, based on the assumption that not all EVs will be on the road
    /// at the same time, even during peak hours. (556,000 * 0.75 = 417,000).
    /// </summary>
    public const int PeakCars = TotalEVs * 75 / 100;

    /// <summary>
    /// Maximum measured congestion (km with critical congestion) from the dataset.
    /// Used to normalize the congestion values.
    /// </summary>
    private const int _maxCongestionKm = 125;

    /// <summary>
    /// Raw congestion data representing km of critically congested road.
    /// Numbers are estimated from the Vejdirektoratet dataset, based on eye estimations.
    /// Source: https://www.vejdirektoratet.dk/side/trafikkens-udvikling-i-tal.
    /// </summary>
    private static readonly int[,] _congestionKm =
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
    /// <param name="hour">The hour of the day (0-23).</param>
    /// <returns>The estimated number of EVs on the road.</returns>
    public static int GetEVsOnRoad(Day day, int hour)
    {
        if (hour < 0 || hour > 23)
        {
            throw new ArgumentOutOfRangeException(nameof(hour), "Hour must be between 0 and 23.");
        }

        if (!Enum.IsDefined(day))
        {
            throw new ArgumentOutOfRangeException(nameof(day), "Day must be between Monday and Sunday.");
        }

        var km = _congestionKm[(int)day, hour];

        var congestionIndex = (float)km / _maxCongestionKm;

        var cars = BaselineCars + ((PeakCars - BaselineCars) * congestionIndex);

        return (int)Math.Min(cars, TotalEVs);
    }
}