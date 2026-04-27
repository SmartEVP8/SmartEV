namespace Engine.test.Grid;

using Core.Shared;
using Engine.Grid;

public class PolygoonerTest
{
    [Fact]
    public void GenerateGrid_CellInsideStationExclusionZone_IsNotSpawnable()
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