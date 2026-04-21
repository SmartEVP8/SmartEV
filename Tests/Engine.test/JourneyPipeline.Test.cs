namespace Engine.test;

using Core.Shared;
using Engine.Grid;
using Engine.Routing;
using Engine.Spawning;

public class JourneyPipelineTests
{
    private static Position Pos() => new(0.0, 0.0);

    private static City MakeCity(string name, int pop) => new(name, Pos(), pop);

    private static GridCell SpawnableCell() => new(spawnable: true, Pos());

    private static GridCell NonSpawnableCell() => new(spawnable: false, Pos());

    private static SpawnGrid MakeGrid(int spawnableCount, int nonSpawnableCount = 0)
    {
        var row = Enumerable.Repeat(SpawnableCell(), spawnableCount)
            .Concat(Enumerable.Repeat(NonSpawnableCell(), nonSpawnableCount))
            .ToList();

        return new SpawnGrid([row], min: new Position(0.0, 0.0), latSize: 1.0, lonSize: 1.0);
    }

    [Fact]
    public void Compute_NoSpawnableCells_ReturnsThrows()
    {
        var grid = MakeGrid(spawnableCount: 0);
        var cities = new List<City> { MakeCity("X", pop: 5000) };

        Assert.Throws<InvalidOperationException>(() => new JourneyPipeline(grid, cities, new StubRouter([])));
    }

    [Fact]
    public void BoundingBox_ReturnsExpectedMinMax_NormalCell()
    {
        var grid = new SpawnGrid([], new Position(0.0, 0.0), latSize: 2.0, lonSize: 4.0);
        var cell = new GridCell(spawnable: true, new Position(10.0, 20.0));

        var (min, max) = grid.GetBoundingBox(cell);

        Assert.Equal(8.0, min.Longitude);
        Assert.Equal(19.0, min.Latitude);
        Assert.Equal(12.0, max.Longitude);
        Assert.Equal(21.0, max.Latitude);
    }

    [Fact]
    public void BoundingBox_ReturnsExpectedMinMax_UnitCell()
    {
        var grid = new SpawnGrid([], new Position(0.0, 0.0), latSize: 1.0, lonSize: 1.0);
        var cell = new GridCell(spawnable: true, new Position(0.0, 0.0));

        var (min, max) = grid.GetBoundingBox(cell);

        Assert.Equal(-0.5, min.Longitude);
        Assert.Equal(-0.5, min.Latitude);
        Assert.Equal(0.5, max.Longitude);
        Assert.Equal(0.5, max.Latitude);
    }

    [Fact]
    public void BoundingBox_ContainsCenterPoint()
    {
        var grid = new SpawnGrid([], new Position(0.0, 0.0), latSize: 2.0, lonSize: 2.0);
        var cell = new GridCell(spawnable: true, new Position(5.0, 5.0));

        var (min, max) = grid.GetBoundingBox(cell);

        Assert.InRange(cell.Centerpoint.Longitude, min.Longitude, max.Longitude);
        Assert.InRange(cell.Centerpoint.Latitude, min.Latitude, max.Latitude);
    }

    [Fact]
    public void GenerateGrid_CellInsideWetPolygon_NotSpawnable()
    {
        var polygon = new List<Position>
    {
        new(-1, -1), new(1, -1), new(1, 1), new(-1, 1),
    };
        var wetPolygon = new List<Position>
    {
        new(-0.1, -0.1), new(0.1, -0.1), new(0.1, 0.1), new(-0.1, 0.1),
    };

        var grid = Polygooner.GenerateGrid(0.05, [polygon], [wetPolygon]);
        var center = grid.Cells.SelectMany(r => r)
            .FirstOrDefault(c => Math.Abs(c.Centerpoint.Longitude) < 0.05
                              && Math.Abs(c.Centerpoint.Latitude) < 0.05);

        Assert.NotNull(center);
        Assert.False(center.Spawnable);
    }

    [Fact]
    public void GenerateGrid_CellOutsideWetPolygon_Spawnable()
    {
        var polygon = new List<Position>
    {
        new(-1, -1), new(1, -1), new(1, 1), new(-1, 1),
    };
        var wetPolygon = new List<Position>
    {
        new(-0.1, -0.1), new(0.1, -0.1), new(0.1, 0.1), new(-0.1, 0.1),
    };

        var grid = Polygooner.GenerateGrid(0.05, [polygon], [wetPolygon]);
        var farCell = grid.Cells.SelectMany(r => r)
            .FirstOrDefault(c => c.Centerpoint.Longitude > 0.5
                              && c.Centerpoint.Latitude > 0.5);

        Assert.NotNull(farCell);
        Assert.True(farCell.Spawnable);
    }

    [Fact]
    public void GenerateGrid_EntireAreaWet_NoCellsSpawnable()
    {
        var polygon = new List<Position>
    {
        new(-1, -1), new(1, -1), new(1, 1), new(-1, 1),
    };
        var wetPolygon = new List<Position>
    {
        new(-2, -2), new(2, -2), new(2, 2), new(-2, 2),
    };

        var grid = Polygooner.GenerateGrid(0.1, [polygon], [wetPolygon]);
        var anySpawnable = grid.Cells.SelectMany(r => r).Any(c => c.Spawnable);

        Assert.False(anySpawnable);
    }

    private class StubRouter(float[] distances) : IMatrixRouter
    {
        private readonly float[] _distances = distances;

        RoutingResult IMatrixRouter.QueryPointsToPoints(double[] srcCoords, double[] dstCoords) => new([], _distances);
    }
}
