namespace Engine.test.Grid;

using Core.Shared;
using Engine.Grid;

public class PolygoonerTest
{
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
        var stationPolygons = new List<Position>
        {
            new(0.8, 0.8),
        };

        var grid = Polygooner.GenerateGrid(0.05, [polygon], [wetPolygon], stationPolygons);
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
        var stationPolygons = new List<Position>
        {
            new(0.8, 0.8),
        };

        var grid = Polygooner.GenerateGrid(0.05, [polygon], [wetPolygon], stationPolygons);
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
        var stationPolygons = new List<Position>
        {
            new(0.8, 0.8),
        };

        var grid = Polygooner.GenerateGrid(0.1, [polygon], [wetPolygon], stationPolygons);
        var anySpawnable = grid.Cells.SelectMany(r => r).Any(c => c.Spawnable);

        Assert.False(anySpawnable);
    }

    [Fact]
    public void GenerateGrid_CellInsideStationPolygon_IsNotSpawnable()
    {
        var stationPosition = new Position(10.0, 56.0);

        // A land polygon large enough to contain the station
        List<List<Position>> landPolygon =
        [
            [
                new(9.0, 55.0),
                new(11.0, 55.0),
                new(11.0, 57.0),
                new(9.0, 57.0),
            ]
        ];

        var grid = Polygooner.GenerateGrid(
            size: 0.001,
            polygons: landPolygon,
            wetPolygons: [],
            stationPositions: [stationPosition]);

        var cell = grid.GetCell(stationPosition);
        Assert.False(cell!.Spawnable, "Cell at station position should not be spawnable");
    }
}