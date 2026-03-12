using Core.Charging;
using Core.Shared;
using Engine.Grid;
using Engine.Parsers;

namespace Engine.test.Grid;

public class SpatialGridTests
{
    private static readonly Random _random = new();

    private SpatialGrid BuildSpatialGrid(IEnumerable<Station> stations)
    {
        var polygons = PolygonParser.Parse(File.ReadAllText("/home/mertz/Coding/SmartEV/data/denmark.polygon.json"));
        var grid = Polygooner.GenerateGrid(0.1, polygons);
        return new SpatialGrid(grid, stations);
    }

    [Fact]
    public void GetStations_SinglePosition_ReturnsSingleStation()
    {
        var station1 = new Station(1, string.Empty, string.Empty, new Position(10.0, 56.0), null, 0, _random);
        var station2 = new Station(2, string.Empty, string.Empty, new Position(10.5, 56.5), null, 0, _random);
        var station3 = new Station(3, string.Empty, string.Empty, new Position(10.3, 56.5), null, 0, _random);

        var sg = BuildSpatialGrid([station1, station2, station3]);

        var result = sg.GetStations(new Position(10.0, 56.0));

        Assert.Single(result);
        Assert.Equal(station1.GetId(), result[0]);
    }

    [Fact]
    public void GetStations_BoundingBox_ReturnsAllStationsInRange()
    {
        var station1 = new Station(1, string.Empty, string.Empty, new Position(10.0, 56.0), null, 0, _random);
        var station2 = new Station(2, string.Empty, string.Empty, new Position(10.5, 56.5), null, 0, _random);
        var station3 = new Station(3, string.Empty, string.Empty, new Position(10.3, 56.5), null, 0, _random);

        var sg = BuildSpatialGrid([station1, station2, station3]);

        var result = sg.GetStations(new Position(10.0, 56.0), new Position(10.5, 56.5));

        Assert.Equal(3, result.Count);
        Assert.Contains(result, s => s == station1.GetId());
        Assert.Contains(result, s => s == station2.GetId());
        Assert.Contains(result, s => s == station3.GetId());
    }
}
