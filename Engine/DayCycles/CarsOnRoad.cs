namespace Engine.DayCycles;

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
    /// Maximum congestion, used to normalize the congestion values.
    /// </summary>
    private const int _maxCongestion = 100;

    private static readonly int[,] _congestion =
    {
        // Sunday
        { 3, 2, 3, 4, 4, 4, 6, 8, 10, 12, 14, 16, 20, 16, 20, 24, 26, 26, 22, 21, 16, 12, 6, 4 },

        // Monday
        { 3, 2, 3, 4, 16, 32, 88, 96, 72, 48, 32, 24, 32, 40, 48, 52, 48, 28, 32, 28, 28, 16, 8, 4 },

        // Tuesday
        { 3, 2, 3, 4, 16, 32, 96, 100, 72, 48, 32, 28, 40, 48, 64, 64, 48, 32, 28, 24, 16, 12, 8, 4 },

        // Wednesday
        { 3, 2, 3, 4, 16, 24, 48, 56, 48, 40, 16, 24, 32, 40, 48, 48, 40, 24, 28, 24, 16, 12, 8, 4 },

        // Thursday
        { 3, 2, 3, 4, 16, 24, 48, 56, 48, 40, 24, 32, 48, 64, 64, 56, 48, 40, 32, 24, 16, 16, 8, 4 },

        // Friday
        { 3, 2, 3, 4, 8, 12, 16, 24, 32, 24, 20, 32, 44, 60, 58, 52, 44, 32, 24, 16, 16, 12, 8, 4 },

        // Saturday
        { 3, 2, 3, 4, 4, 4, 6, 8, 12, 14, 16, 24, 32, 32, 24, 20, 22, 20, 21, 20, 16, 12, 8, 4 },
    };

    /// <summary>
    /// Gets the estimated number of EVs on the road for a specific day and hour.
    /// </summary>
    /// <param name="day">The day of the week.</param>
    /// <param name="hour">The hour of the day (0-23).</param>
    /// <returns>The estimated number of EVs on the road.</returns>
    public static int GetEVsOnRoad(DayOfWeek day, int hour)
    {
        var carCongestion = _congestion[(int)day, hour];
        var cars = BaselineCars + ((PeakCars - BaselineCars) * carCongestion / _maxCongestion);

        return Math.Min(cars, TotalEVs);
    }
}
