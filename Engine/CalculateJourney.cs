namespace Engine;

using Core.Routing;
using Core.Shared;
using Core.Spawning;

/// <summary>
/// Calculates the spawn chances for cities based on their population and distance to grid centers.
/// The process involves three main steps:
/// 1. CalculateDistance: Uses OSRM to compute distances from grid centers to cities.
/// 2. CalculateSpawnChance: Computes spawn chances as population divided by distance, scaled by a given factor.
/// 3. NormalizeSpawnChance: Normalizes spawn chances so that they sum to 1 for each grid center, representing probabilities.
/// </summary>
/// <remarks>
/// This class is designed to be used in a headless environment for testing and simulation purposes.
/// It relies on the OSRMRouter for distance calculations and the City class for city data.
/// </remarks>
public class CalculateJourney
{
    /// <summary>
    /// Calculates the spawn chances for cities based on their population and distance to grid centers.
    /// </summary>
    /// <param name="gridCenterPosition">A list of positions representing the centers of the grids.</param>
    /// <param name="scaler">A scaling factor to adjust the influence of population on spawn chances.</param>
    /// <param name="cities">A list of City objects containing information about each city, including name, position, and population.</param>
    /// <returns>A list of tuples, where each tuple contains a grid index and an matrix of city names with their corresponding normalized spawn chances.</returns>
    public List<(int, (string, float)[])> CalculateSpawn(List<Position> gridCenterPosition, float scaler, List<City> cities)
    {
    // Get distances from CalculateDistance
    var distanceData = CalculateDistance(gridCenterPosition, cities);

    // Calculate spawn chance as population divided by distance, multiplied by scaler
    var spawnData = CalculateSpawnChance(distanceData, cities, scaler);
    return NormalizeSpawnChance(spawnData);
    }

    private List<(int GridIndex, (string CityName, float Distance)[])> CalculateDistance(List<Position> gridCenterPosition, List<City> cities)
    {
        var distanceList = new List<(int, (string, float)[])>();
        var routing = new OSRMRouter("../data/output.osrm");
        var gridpositions = gridCenterPosition.SelectMany(p => new double[] { p.Longitude, p.Latitude }).ToArray();
        var citypositions = cities.SelectMany(c => new double[] { c.Position.Longitude, c.Position.Latitude }).ToArray();

        var (_, dis) = routing.QueryPointsToPoints(gridpositions, gridCenterPosition.Count, citypositions, cities.Count);
        for (var j = 0; j < gridCenterPosition.Count; j++)
        {
            var distanceMatrix = new (string, float)[cities.Count];
            for (var i = 0; i < cities.Count; i++)
            {
                var distance = dis[i + (cities.Count * j)];
                distanceMatrix[i] = (cities[i].Name, distance);
            }

            distanceList.Add((j, distanceMatrix));
        }

        return distanceList;
    }

    private List<(int GridIndex, (string CityName, float SpawnChance)[])> CalculateSpawnChance(List<(int GridIndex, (string CityName, float Distance)[])> distanceData, List<City> cities, float scaler)
    {
    var spawnChanceList = new List<(int, (string, float)[])>();
    foreach (var (gridIndex, cityDistances) in distanceData)
    {
        var spawnChances = new (string, float)[cityDistances.Length];
        for (var i = 0; i < cityDistances.Length; i++)
        {
            var cityName = cityDistances[i].CityName;
            var distance = cityDistances[i].Distance;
            var population = cities.First(c => c.Name == cityName).Population;

            // Handle zero or very small distances to avoid division by zero
            var adjustedDistance = Math.Max(distance, 1.0f); // Minimum distance of 1 meter

            var spawnChance = Math.Pow(population, scaler) / Math.Pow(adjustedDistance, 0.8);
            spawnChances[i] = (cityName, (float)spawnChance);
        }

        spawnChanceList.Add((gridIndex, spawnChances));
    }

    return spawnChanceList;
    }

    private List<(int GridIndex, (string CityName, float SpawnChance)[])> NormalizeSpawnChance(List<(int GridIndex, (string CityName, float SpawnChance)[])> spawnChanceData)
    {
        var normalizedList = new List<(int, (string, float)[])>();

        foreach (var (gridIndex, citySpawnChances) in spawnChanceData)
        {
            // Calculate total spawn chance for THIS grid only
            var gridTotalSpawnChance = citySpawnChances.Sum(c => c.SpawnChance);

            // Normalize so this grid's cities sum to 1 (100%)
            var normalizedSpawnChances = citySpawnChances
                .Select(c => (c.CityName, c.SpawnChance / gridTotalSpawnChance))
                .ToArray();

            normalizedList.Add((gridIndex, normalizedSpawnChances));
        }

        return normalizedList;
    }
}
