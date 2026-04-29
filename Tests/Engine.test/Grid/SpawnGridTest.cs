namespace Engine.Grid.Tests;

using Core.Shared;

public class SpawnGridTest
{
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