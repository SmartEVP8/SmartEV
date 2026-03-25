namespace Engine.test.Grid;

using Core.Charging;
using Core.Shared;
using Engine.Grid;
using Engine.Parsers;
using Engine.test.Builders;

public class SpatialGridTests
{
    public SpatialGrid BuildSpatialGrid(Dictionary<ushort, Station> stations)
    {
        var path = AppContext.GetData("GridPath") as string
                    ?? throw new InvalidOperationException("GridPath not set in project.");

        var polygons = PolygonParser.Parse(File.ReadAllText(path));
        var grid = Polygooner.GenerateGrid(0.1, polygons);
        return new SpatialGrid(grid, stations);
    }

    [Fact]
    public void GetStations_Along_Polyline()
    {
        var station1 = TestData.Station(1, new Position(10.0, 56.0));
        var station2 = TestData.Station(2, new Position(10.5, 56.5));
        var station3 = TestData.Station(3, new Position(10.3, 56.5));

        var sg = BuildSpatialGrid(new Dictionary<ushort, Station> { { station1.Id, station1 }, { station2.Id, station2 }, { station3.Id, station3 } });

        var path = new Paths([new Position(10.0, 56.0), new Position(10.5, 56.5)]);
        var result = sg.GetStationsAlongPolyline(path, 20);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, s => s == station1.Id);
        Assert.Contains(result, s => s == station2.Id);
        Assert.Contains(result, s => s == station3.Id);
    }

    [Fact]
    public void GetStationsAlongPolyline_StationOutsideRadius_NotReturned()
    {
        var nearby = TestData.Station(1, new Position(10.2, 56.15));
        var farAway = TestData.Station(2, new Position(12.5, 55.6));
        var sg = BuildSpatialGrid(new Dictionary<ushort, Station> { { nearby.Id, nearby }, { farAway.Id, farAway } });
        var path = new Paths([new Position(10.0, 56.15), new Position(10.5, 56.15)]);
        var result = sg.GetStationsAlongPolyline(path, 15);

        Assert.Contains(result, s => s == nearby.Id);
        Assert.DoesNotContain(result, s => s == farAway.Id);
    }

    [Fact]
    public void GetStationsAlongPolyline_StationPerpendicularToSegment_IsFound()
    {
        var station = TestData.Station(1, new Position(10.2, 56.15));
        var sg = BuildSpatialGrid(new Dictionary<ushort, Station> { { station.Id, station } });
        var path = new Paths([new Position(10.0, 56.15), new Position(10.5, 56.15)]);
        var result = sg.GetStationsAlongPolyline(path, 15);

        Assert.Contains(result, s => s == station.Id);
    }

    [Fact]
    public void GetStationsAlongPolyline_NoDuplicates_WhenStationNearMultipleSegments()
    {
        var station = TestData.Station(1, new Position(10.2, 56.15));
        var sg = BuildSpatialGrid(new Dictionary<ushort, Station> { { station.Id, station } });
        var path = new Paths([
            new Position(10.0, 56.15),
            new Position(10.2, 56.15),
            new Position(10.5, 56.15)
        ]);
        var result = sg.GetStationsAlongPolyline(path, 15);
        Assert.Single(result);
    }
}