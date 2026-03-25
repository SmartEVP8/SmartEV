namespace Engine.Grid;

using Core.Shared;

/// <summary>
/// A grid that contains "gravity cells" which have information about the distance and population of nearby cities.
/// </summary>
/// <param name="cells">The list of gravity cells.</param>
/// <param name="cityCenters">The positions of the city centers.</param>
/// <param name="halfLat">The half latitude of the grid.</param>
/// <param name="halfLon">The half longitude of the grid.</param>
internal class GravityGrid(List<List<GravityCell>> cells, Position[] cityCenters, double halfLat, double halfLon)
{
    /// <summary>Gets the list of gravity cells.</summary>
    public List<List<GravityCell>> Cells { get; } = cells;

    /// <summary>Gets the half latitude of the grid.</summary>
    public double HalfLat { get; } = halfLat;

    /// <summary>Gets the half longitude of the grid.</summary>
    public double HalfLon { get; } = halfLon;

    /// <summary>Gets the positions of the city centers.</summary>
    public Position[] CityCenters { get; } = cityCenters;

    /// <summary>Gets the center points of all cells.</summary>
    public Position[] CellCenters { get; } = [.. cells.SelectMany(g => g).Select(c => c.Centerpoint)];
}

/// <summary>
/// A gravity cell that contains information about the distance and population of nearby cities.
/// </summary>
/// <param name="centerPoint">The center point of the gravity cell.</param>
/// <param name="cityInfo">The information about nearby cities.</param>
internal class GravityCell(Position centerPoint, List<CityInfo> cityInfo)
{
    /// <summary>Gets the center point of the gravity cell.</summary>
    public Position Centerpoint { get; } = centerPoint;

    /// <summary>Gets the city info for nearby cities.</summary>
    public List<CityInfo> CityInfo { get; } = cityInfo;
}

/// <summary>
/// A struct that contains information about a city, including its name, distance to the city, and population.
/// </summary>
/// <param name="cityName">The name of the city.</param>
/// <param name="distToCity">The distance to the city.</param>
/// <param name="population">The population of the city.</param>
internal readonly struct CityInfo(string cityName, float distToCity, float population)
{
    /// <summary>Gets the name of the city.</summary>
    public string CityName { get; } = cityName;

    /// <summary>Gets the distance to the city.</summary>
    public float DistToCity { get; } = distToCity;

    /// <summary>Gets the population of the city.</summary>
    public float Population { get; } = population;
}