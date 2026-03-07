namespace Engine;

using Core.Routing;
using Core.Spawning;
using Engine.Grid;

public class CalculateJourney
{
    public SpawnableGrid CalculateDistance(SpawnGrid grid, List<City> cities, OSRMRouter router)
    {
        var newGrid = new List<SpawnableCells>[grid.Cells.Count];

        Parallel.For(0, grid.Cells.Count, index =>
        {
            var cell = grid.Cells[index];
            var distance = ComputeAllDistances(cities, router, cell);

            var cellData = new List<SpawnableCells>(cell.Count);
            for (var i = 0; i < cell.Count; i++)
            {
                var distanceList = new List<CityInfo>(cities.Count);
                if (!cell[i].Spawnable)
                {
                    cellData.Add(new SpawnableCells(0f, cell[i].Midpoint, distanceList));
                    continue;
                }

                for (var j = 0; j < cities.Count; j++)
                {
                    var distToCity = distance[j + (cities.Count * i)];
                    if (distToCity < 0) continue;
                    distanceList.Add(new CityInfo(cities[j].Name, distToCity, 0f));
                }

                cellData.Add(new SpawnableCells(1f, cell[i].Midpoint, distanceList));
            }

            newGrid[index] = cellData;
        });

        return new SpawnableGrid(newGrid.ToList());
    }

    public SpawnableGrid CalculateDestChances(SpawnableGrid grid, List<City> cities, float scaler)
    {
        foreach (var cell in grid.SpawnableCells.SelectMany(cell => cell))
        {
            for (var i = 0; i < cell.CityInfo.Count; i++)
            {
                if (cell.spawnChance is <= 0) continue; // Skip grids with zero spawn chance
                var cityName = cell.CityInfo[i].CityName;
                var distance = cell.CityInfo[i].DistToCity;
                var population = cities.First(c => c.Name == cityName).Population;

                // Handle zero or very small distances to avoid division by zero
                var adjustedDistance = Math.Max(distance, 1.0f); // Minimum distance of 1 meter
                var destChance = (float)(Math.Pow(population, scaler) / Math.Pow(adjustedDistance, 0.8));

                cell.CityInfo[i] = new CityInfo(cityName, distance, destChance);
            }
        }

        return grid;
    }

    public AliasSampler CalculateSpawnRate(SpawnableGrid grid)
    {
        var cells = grid.SpawnableCells
            .SelectMany(g => g)
            .Where(c => c.spawnChance > 0)
            .ToList();

        var weights = new float[cells.Count];

        for (var i = 0; i < cells.Count; i++)
            weights[i] = cells[i].CityInfo.Sum(ci => ci.DestChance);

        return new AliasSampler(weights);
    }

    private static float[] ComputeAllDistances(List<City> cities, OSRMRouter router, List<GridCell> cells)
    {
        var cityPositions = cities
                .SelectMany(c => new double[] { c.Position.Longitude, c.Position.Latitude })
                .ToArray();

        var gridCenters = cells
                .SelectMany(g => new double[] { g.Midpoint.Longitude, g.Midpoint.Latitude })
                .ToArray();

        var (_, distances) = router.QueryPointsToPoints(gridCenters, cells.Count, cityPositions, cities.Count);
        return distances;
    }
}
