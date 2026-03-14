using Core.Shared;
using Engine;
using Engine.Grid;
using Engine.Routing;
using Engine.Spawning;

public class JourneyPipelineTests
{
    private class StubRouter : IMatrixRouter
    {
        private readonly float[] _distances;

        public StubRouter(float[] distances) => _distances = distances;

        public (float[], float[]) QueryPointsToPoints(double[] origins, double[] destinations)
            => ([], _distances);
    }

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
    public void Compute_ReturnsOneDestinationSamplerPerSpawnableCell()
    {
        var grid = MakeGrid(spawnableCount: 2);
        var cities = new List<City> { MakeCity("CityA", pop: 10000) };

        var pipeline = new JourneyPipeline(grid, cities, new StubRouter([500f, 1000f]));
        var samplers = pipeline.Compute(scaler: 1.0f);

        Assert.Equal(2, samplers.DestinationSamplers.Length);
    }

    [Fact]
    public void BuildSpawnableGrid_ExcludesNonSpawnableCells()
    {
        var grid = MakeGrid(spawnableCount: 1, nonSpawnableCount: 3);
        var cities = new List<City> { MakeCity("X", pop: 5000) };

        var pipeline = new JourneyPipeline(grid, cities, new StubRouter([300f]));
        var samplers = pipeline.Compute(scaler: 1.0f);

        Assert.Single(samplers.DestinationSamplers);
    }

    [Fact]
    public void BuildSpawnableGrid_SkipsCitiesWithNegativeDistance()
    {
        var grid = MakeGrid(spawnableCount: 1);
        var cities = new List<City>
        {
            MakeCity("Reachable", pop: 5000),
            MakeCity("Unreachable", pop: 9999)
        };

        var pipeline = new JourneyPipeline(grid, cities, new StubRouter([500f, -1f]));
        var samplers = pipeline.Compute(scaler: 1.0f);

        Assert.Single(samplers.DestinationSamplers);
    }

    [Fact]
    public void Compute_HigherScaler_IncreasesWeightOfLargerCity()
    {
        var grid = MakeGrid(spawnableCount: 1);
        var cities = new List<City>
        {
            MakeCity("Big", pop: 1_000_000),
            MakeCity("Small", pop: 1_000)
        };

        var pipeline = new JourneyPipeline(grid, cities, new StubRouter([500f, 500f]));
        var rng = new Random(42);

        var lowSamplers = pipeline.Compute(scaler: 0.5f);
        var highSamplers = pipeline.Compute(scaler: 2.0f);

        var lowCounts = new int[2];
        var highCounts = new int[2];

        for (var i = 0; i < 10_000; i++)
        {
            lowCounts[lowSamplers.DestinationSamplers[0].Sample(rng)]++;
            highCounts[highSamplers.DestinationSamplers[0].Sample(rng)]++;
        }

        var lowBigCityRatio = (double)lowCounts[0] / lowCounts[1];
        var highBigCityRatio = (double)highCounts[0] / highCounts[1];

        Assert.True(
            highBigCityRatio > lowBigCityRatio * 10,
            $"Expected higher scaler to amplify big city dominance. Low: {lowBigCityRatio:F2}, High: {highBigCityRatio:F2}");
    }

    [Fact]
    public void Compute_NoSpawnableCells_ReturnsNull()
    {
        var grid = MakeGrid(spawnableCount: 0);
        var cities = new List<City> { MakeCity("X", pop: 5000) };

        var pipeline = new JourneyPipeline(grid, cities, new StubRouter([]));

        Assert.Null(pipeline.Compute(scaler: 1.0f));
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
}
