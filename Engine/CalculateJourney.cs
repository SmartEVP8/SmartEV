namespace Engine;

using Core.Events;
using Core.Routing;
using Core.Shared;
using Core.Spawning;


/// <summary>
/// Calculates the chances a EV to drive to a city based on their population and distance to grid centers.
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
    /// Calculates the chances for a EV to drive to a city based on the city's population and distance to grid centers.
    /// </summary>
    /// <param name="grids">A list of all grids of Denmark.</param>
    /// <param name="scaler">A scaling factor to adjust the influence of population on spawn chances.</param>
    /// <param name="cities">A list of City objects containing information about each city, including name, position, and population.</param>
    /// <param name="router">An instance of OSRMRouter used to calculate distances between grid centers and cities.</param>
    /// <returns>A list of tuples, where each tuple contains a grid index and an matrix of city names with their corresponding normalized spawn chances.</returns>
    public SpawnableGrid CalculateDestChance(SpawnableGrid grids, float scaler, List<City> cities, OSRMRouter router)
    {
        var newGrids = new SpawnableGrid([]);
        foreach (var list in grids.SpawnableCells)
        {

            // Step 1: Calculate distances from grid centers to cities
            var distanceData = CalculateDistance(list, cities, router);

            // Step 2: Calculate chances based on population and distance
            var chanceData = CalculateChances(distanceData, cities, scaler);

            // Step 3: Normalize chances to get probabilities
            var result = NormalizeChances(chanceData);
            newGrids.SpawnableCells.Add(result);
        }

        return newGrids;
    }

    private List<SpawnableGridCells> CalculateDistance(List<SpawnableGridCells> grids, List<City> cities, OSRMRouter router)
    {
        var gridCenters = grids.SelectMany(g => new double[]
        {
            g.midpoint.Longitude, g.midpoint.Latitude,
        }).ToArray();
        var citypositions = cities.SelectMany(c => new double[] { c.Position.Longitude, c.Position.Latitude }).ToArray();

        var (_, dis) = router.QueryPointsToPoints(gridCenters, grids.Count, citypositions, cities.Count);
        for (var j = 0; j < grids.Count; j++)
        {
            if (grids[j].spawnChance <= 0) continue; // Skip grids with zero spawn chance
            var distanceMatrix = new (string, float)[cities.Count];
            for (var i = 0; i < cities.Count; i++)
            {
                if (dis[i + (cities.Count * j)] < 0) continue;
                var distance = dis[i + (cities.Count * j)];
                distanceMatrix[i] = (cities[i].Name, distance);
            }

            grids[j].CityDestChances = distanceMatrix.Where(d => d != default).ToList();
        }

        return grids;
    }

    private List<SpawnableGridCells> CalculateChances(List<SpawnableGridCells> grids, List<City> cities, float scaler)
    {
    var chanceList = new List<(int, (string, float)[])>();
    foreach (var grid in grids)
    {
        for (var i = 0; i < grid.CityDestChances.Count; i++)
        {
            var cityName = grid.CityDestChances[i].CityName;
            var distance = grid.CityDestChances[i].CitySpawnChance; // This is actually the distance from CalculateDistance
            var population = cities.First(c => c.Name == cityName).Population;

            // Handle zero or very small distances to avoid division by zero
            var adjustedDistance = Math.Max(distance, 1.0f); // Minimum distance of 1 meter

            var spawnChance = Math.Pow(population, scaler) / Math.Pow(adjustedDistance, 0.8);
            grid.CityDestChances[i] = (cityName, (float)spawnChance);
        }
    }

    return grids;
    }

    private List<SpawnableGridCells> NormalizeChances(List<SpawnableGridCells> grids)
    {
        foreach (var grid in grids)
        {
            // Calculate total spawn chance for THIS grid only
            var gridTotalChance = grid.CityDestChances.Sum(c => c.CitySpawnChance);

            // Normalize so this grid's cities sum to 1 (100%)
            var normalizedChances = grid.CityDestChances
                .Select(c => (c.CityName, c.CitySpawnChance / gridTotalChance))
                .ToList();
            grid.CityDestChances = normalizedChances;
        }
        return grids;
    }
}
