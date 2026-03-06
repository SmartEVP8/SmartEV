namespace Core.DayCycles;

/// <summary>
/// This class provides estimated numbers of EVs on the road for each hour of each day of the week
/// based on congestion data and total registered EVs in Denmark.
/// </summary>
public static class Cars
{
    /// <summary>
    /// Total registered EVs in Denmark according to DST - January 2026 (Rounded down).
    /// Source: https://www.dst.dk/da/Statistik/udgivelser/NytHtml?cid=51885.
    /// </summary>
    public static readonly int TotalEVs = 556000;

    /// <summary>
    /// Minimum number of EVs expected on the road even with almost no congestion.
    /// Estimated as ~3% of total EVs. (556,000 * 0.03 = 16,680).
    /// </summary>
    public static readonly int BaselineCars = 16680;

    /// <summary>
    /// Maximum number of EVs on the road during peak congestion.
    /// Estimated as ~75% of total EVs, based on the assumption that not all EVs will be on the road 
    /// at the same time, even during peak hours. (556,000 * 0.75 = 417,000).
    /// </summary>
    public static readonly int PeakCars = 417000;
}