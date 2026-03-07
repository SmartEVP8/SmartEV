namespace Engine;

using Core.Routing;
using Core.Spawning;
using Engine.Grid;

public class CalculateJourney
{
    public SpawnableGrid CalculateDistance(SpawnGrid grid, List<City> cities, OSRMRouter router)
    {
        var newGrid = new List<SpawnableCell>[grid.Cells.Count];

        Parallel.For(0, grid.Cells.Count, index =>
        {
            var cell = grid.Cells[index];
            var distance = ComputeAllDistances(cities, router, cell);

            var cellData = new List<SpawnableCell>(cell.Count);
            for (var i = 0; i < cell.Count; i++)
            {
                var distanceList = new List<CityInfo>(cities.Count);
                if (!cell[i].Spawnable)
                {
                    cellData.Add(new SpawnableCell(0f, cell[i].Centerpoint, distanceList));
                    continue;
                }

                for (var j = 0; j < cities.Count; j++)
                {
                    var distToCity = distance[j + (cities.Count * i)];
                    if (distToCity < 0) continue;
                    distanceList.Add(new CityInfo(cities[j].Name, distToCity, cities[j].Population));
                }

                cellData.Add(new SpawnableCell(1f, cell[i].Centerpoint, distanceList));
            }

            newGrid[index] = cellData;
        });

        return new SpawnableGrid(newGrid.ToList());
    }

    public SpawnableGrid CalculateDestChances(SpawnableGrid grid, float scaler = 1f)
    {
        foreach (var cell in grid.SpawnableCells.SelectMany(cell => cell))
        {
            cell.CitySampler = new AliasSampler(cell.CityInfo.Select(ci => CalculatePopulationDistanceWeight(ci, scaler)).ToArray());
        }

        return grid;
    }

    public AliasSampler CalculateSpawnRate(SpawnableGrid grid, float scaler = 1f)
    {
        var cells = grid.SpawnableCells
            .SelectMany(g => g)
            .ToList();

        var weights = new float[cells.Count];

        for (var i = 0; i < cells.Count; i++)
            weights[i] = cells[i].CityInfo.Sum(ci => CalculatePopulationDistanceWeight(ci, scaler));

        return new AliasSampler(weights);
    }

    private static float CalculatePopulationDistanceWeight(CityInfo city, float scaler)
    {
        var adjustedDistance = Math.Max(city.DistToCity, 1.0f); // Minimum distance of 1 meter
        return (float)(Math.Pow(city.Population, scaler) / Math.Pow(adjustedDistance, 0.8));
    }

    private static float[] ComputeAllDistances(List<City> cities, OSRMRouter router, List<GridCell> cells)
    {
        var cityPositions = cities
                .SelectMany(c => new double[] { c.Position.Longitude, c.Position.Latitude })
                .ToArray();

        var gridCenters = cells
                .SelectMany(g => new double[] { g.Centerpoint.Longitude, g.Centerpoint.Latitude })
                .ToArray();

        var (_, distances) = router.QueryPointsToPoints(gridCenters, cells.Count, cityPositions, cities.Count);
        return distances;
    }
}
