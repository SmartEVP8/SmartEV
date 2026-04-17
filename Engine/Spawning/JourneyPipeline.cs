namespace Engine.Spawning;

using Core.Shared;
using Engine.Grid;
using Engine.Routing;

/// <summary>
/// JourneyPipeline computes the sampling distributions for source and destination points
/// based on a grid of spawnable cells and a list of cities.
/// It uses a gravity model to weight the influence of each city on the spawnable cells,
/// taking into account both the population of the cities and their distance from the cells.
/// </summary>
public class JourneyPipeline
{
    private readonly GravityGrid _grid;

    /// <summary>
    /// Initializes a new instance of the <see cref="JourneyPipeline"/> class.
    /// Precomputes distances from each spawnable cell to each city and builds a SpawnableGrid that includes this information.
    /// </summary>
    /// <param name="grid">Controls which cells are spawnable. Non spawnable grid cells get 0% probability.</param>
    /// <param name="cities">Used to compute weights for grid cells.</param>
    /// <param name="router">Computes matrix destination table.</param>
    public JourneyPipeline(SpawnGrid grid, List<City> cities, IMatrixRouter router)
        => _grid = BuildGravityGrid(grid, cities, router);

    /// <summary>
    /// Computes the sampling distributions for source and destination points based on the gravity model.
    /// </summary>
    /// <param name="populationScaler">Influence of city population on the gravity weight.
    /// A higher scaler increases the weight of larger cities, while a lower scaler reduces it.
    /// </param>
    /// <param name="distanceScaler">Influence of city distance on the gravity weight.
    /// A higher scaler increases the weight of closer cities, while a lower scaler reduces it.
    /// </param>
    /// <param name="wetPolygons">List of wet polygons used to ensure that sampled positions do not fall within wet areas.</param>
    /// <returns>Simulation samplers for source and destinations. If no cells are spawnable returns null.</returns>
    public JourneySamplers Compute(float populationScaler, float distanceScaler, List<List<Position>> wetPolygons)
    {
        var cells = _grid.Cells
            .SelectMany(g => g)
            .ToList();

        var sourceWeights = cells
            .Select(c => c.CityInfo.Sum(ci => GravityWeight(ci, populationScaler, distanceScaler)))
            .ToArray();

        var destinationSamplers = cells
            .Select(c => new AliasSampler(
                [.. c.CityInfo.Select(ci => GravityWeight(ci, populationScaler, distanceScaler))]))
            .ToArray();

        return new JourneySamplers(
            new AliasSampler(sourceWeights),
            destinationSamplers,
            _grid.CellCenters,
            _grid.CityCenters,
            _grid.HalfLat,
            _grid.HalfLon,
            wetPolygons);
    }

    private static float GravityWeight(CityInfo city, float populationScaler, float distanceScaler)
    {
        var distance = Math.Max(city.DistToCity, 1.0f);
        return (float)(Math.Pow(city.Population, populationScaler) / Math.Pow(distance, distanceScaler));
    }

    /// <summary>
    /// Builds a GravityGrid from the SpawnGrid and the list of cities.
    /// For each spawnable cell, it computes the distance to each city and stores this information in the GravityGrid.
    /// </summary>
    /// <param name="grid">Spawngrid determines if cells are spawnable.</param>
    /// <param name="cities">List of cities used to compute gravity weight.</param>
    /// <param name="router">Computes matrix destination table.</param>
    /// <returns>The gravity grid.</returns>
    private GravityGrid BuildGravityGrid(SpawnGrid grid, List<City> cities, IMatrixRouter router)
    {
        var allCells = grid.Cells.SelectMany(g => g).ToList();
        var distances = ComputeAllDistances(cities, router, allCells);

        var newGrid = new List<GravityCell>[grid.Cells.Count];
        var cellIndex = 0;
        for (var rowIndex = 0; rowIndex < grid.Cells.Count; rowIndex++)
        {
            var row = grid.Cells[rowIndex];
            var cellData = new List<GravityCell>(row.Count);
            for (var i = 0; i < row.Count; i++, cellIndex++)
            {
                if (!row[i].Spawnable)
                    continue;

                var cityInfo = cities
                    .Select((c, j) => (c, dist: distances[j + (cities.Count * cellIndex)]))
                    .Where(x => x.dist >= 0)
                    .Select(x => new CityInfo(x.c.Name, x.dist, x.c.Population))
                    .ToList();

                if (cityInfo.Count > 0)
                    cellData.Add(new GravityCell(row[i].Centerpoint, cityInfo));
            }

            newGrid[rowIndex] = cellData;
        }

        if (!newGrid.Any(row => row.Count > 0))
            throw new InvalidOperationException("No spawnable cells with city info");

        var cityCenters = cities.Select(c => c.Position).ToArray();

        return new GravityGrid([.. newGrid], cityCenters, grid.LatSize / 2, grid.LonSize / 2);
    }

    /// <summary>
    /// Queries the distance matrix between a row of grid cells and all cities.
    /// Returns a flat array indexed as [cityIndex + (cityCount * cellIndex)].
    /// </summary>
    private float[] ComputeAllDistances(List<City> cities, IMatrixRouter router, List<GridCell> cells)
    {
        var cityPositions = cities
            .SelectMany(c => new double[] { c.Position.Longitude, c.Position.Latitude })
            .ToArray();

        var gridCenters = cells
            .SelectMany(g => new double[] { g.Centerpoint.Longitude, g.Centerpoint.Latitude })
            .ToArray();

        var (_, distances) = router.QueryPointsToPoints(gridCenters, cityPositions);
        return distances;
    }
}