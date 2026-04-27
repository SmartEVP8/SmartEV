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

    private class StubRouter(float[] distances) : IMatrixRouter
    {
        private readonly float[] _distances = distances;

        RoutingResult IMatrixRouter.QueryPointsToPoints(double[] srcCoords, double[] dstCoords) => new([], _distances);
    }
}
