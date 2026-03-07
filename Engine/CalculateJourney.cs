namespace Engine;

using Core.Routing;
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
    /*
    /// <summary>
    /// Calculates the chances for a EV to drive to a city based on the city's population and distance to grid centers.
    /// </summary>
    /// <param name="grids">A list of all grids of Denmark.</param>
    /// <param name="scaler">A scaling factor to adjust the influence of population on spawn chances.</param>
    /// <param name="cities">A list of City objects containing information about each city, including name, position, and population.</param>
    /// <param name="router">An instance of OSRMRouter used to calculate distances between grid centers and cities.</param>
    /// <returns>A list of tuples, where each tuple contains a grid index and an matrix of city names with their corresponding normalized spawn chances.</returns>
    public SpawnableGrid CalculateDestChance(SpawnableGrid grids,  List<City> cities, OSRMRouter router)
    {
        var newGrids = new SpawnableGrid([]);
        Parallel.ForEach(grids.SpawnableCells, (list) =>
        {
            // Step 1: Calculate distances from grid centers to cities
            var distanceData = CalculateDistance(list, cities, router);

            // Step 2: Calculate chances based on population and distance
            // var chanceData = CalculateChances(distanceData, cities, scaler);

            // Step 3: Normalize chances to get probabilities
            // var result = NormalizeChances(chanceData);
            newGrids.SpawnableCells.Add(distanceData);
        });

        return newGrids;
    }
    */

    public SpawnableGrid CalculateDistance(SpawnableGrid grid, List<City> cities, OSRMRouter router)
    {

        var newGrids = new SpawnableGrid([]);
        Parallel.ForEach(grid.SpawnableCells, (list) =>
        {
            var gridCenters = list.SelectMany(g => new double[]
                {
                    g.midpoint.Longitude, g.midpoint.Latitude,
                }).ToArray();
            var citypositions = cities.SelectMany(c => new double[] { c.Position.Longitude, c.Position.Latitude }).ToArray();
            var (_, distance) = router.QueryPointsToPoints(gridCenters, list.Count, citypositions, cities.Count);

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].spawnChance <= 0) continue; // Skip grids with zero spawn chance
                var distanceMatrix = new (string, float, float)[cities.Count];
                for (var j = 0; j < cities.Count; j++)
                {
                    if (distance[j + (cities.Count * i)] < 0) continue;
                    distanceMatrix[j] = (cities[j].Name, distance[j + (cities.Count * i)], 0f);
                }

                list[i].CityInfo = distanceMatrix.Where(d => d != default).ToList();
            }
        });

        return grid;
    }

    public SpawnableGrid CalculateChances(SpawnableGrid grid, List<City> cities, float scaler)
    {
    foreach (var list in grid.SpawnableCells.SelectMany(g => g))
    {
        var cityChances = new (string, float, float)[list.CityInfo.Count];
        for (var i = 0; i < list.CityInfo.Count; i++)
        {
            if (list.spawnChance is <= 0) continue; // Skip grids with zero spawn chance
            var cityName = list.CityInfo[i].CityName;
            var distance = list.CityInfo[i].DistToCity; // This is actually the distance from CalculateDistance
            var population = cities.First(c => c.Name == cityName).Population;

            // Handle zero or very small distances to avoid division by zero
            var adjustedDistance = Math.Max(distance, 1.0f); // Minimum distance of 1 meter

            var destChance = (float)(Math.Pow(population, scaler) / Math.Pow(adjustedDistance, 0.8));
            list.CityInfo[i] = (cityName, distance, destChance);
        }
    }

    return grid;
    }

    public SpawnableGrid CalculateSpawnRate(SpawnableGrid grid)
    {
        var totalDestChance = grid.SpawnableCells.SelectMany(g => g).Sum(c => c.CityInfo.Sum(ci => ci.DestChance));

        foreach (var cell in grid.SpawnableCells.SelectMany(g => g))
        {
           var allDestChance = cell.CityInfo.Sum(ci => ci.DestChance);
           cell.spawnChance = allDestChance / totalDestChance; // Normalize to get spawn chance as a probability
        }

        return grid;
    }

/*
    private List<SpawnableGridCells> NormalizeChances(List<SpawnableGridCells> grids)
    {
        foreach (var grid in grids)
        {
            if (grid.spawnChance is <= 0) continue; // Skip grids with zero spawn chance

            // Calculate total spawn chance for THIS grid only
            var gridTotalChance = grid.CityChances.Sum(c => c.CityDestChance);

            // Normalize so this grid's cities sum to 1 (100%)
            var normalizedChances = grid.CityChances
                .Select(c => (c.CityName, c.CityDestChance / gridTotalChance))
                .ToList();
            grid.CityChances = normalizedChances;
        }

        return grids;
    }
    */
}
