namespace Engine.test.Grid;

using Core.Charging;
using Core.Shared;
using Engine.Grid;
using Engine.Parsers;

using Engine.test.Builders;
using Core.test.Builders;

public class SpatialGridTests
{
    [Fact]
    public void GetStations_Along_Polyline()
    {
        var station1 = CoreTestData.Station(1, new(10.0, 56.0));
        var station2 = CoreTestData.Station(2, new(10.5, 56.5));
        var station3 = CoreTestData.Station(3, new(10.3, 56.5));

        var sg = EngineTestData.BuildSpatialGrid(new Dictionary<ushort, Station> { { station1.Id, station1 }, { station2.Id, station2 }, { station3.Id, station3 } });

        var path = new List<Position>([new(10.0, 56.0), new(10.5, 56.5)]);
        var result = sg.GetStationsAlongPolyline(path, 20);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, s => s == station1.Id);
        Assert.Contains(result, s => s == station2.Id);
        Assert.Contains(result, s => s == station3.Id);
    }

    [Fact]
    public void GetStationsAlongPolyline_StationOutsideRadius_NotReturned()
    {
        var nearby = CoreTestData.Station(1, new(10.2, 56.15));
        var farAway = CoreTestData.Station(2, new(12.5, 55.6));
        var sg = EngineTestData.BuildSpatialGrid(new Dictionary<ushort, Station> { { nearby.Id, nearby }, { farAway.Id, farAway } });
        var path = new List<Position>([new(10.0, 56.15), new(10.5, 56.15)]);
        var result = sg.GetStationsAlongPolyline(path, 15);

        Assert.Contains(result, s => s == nearby.Id);
        Assert.DoesNotContain(result, s => s == farAway.Id);
    }

    [Fact]
    public void GetStationsAlongPolyline_StationPerpendicularToSegment_IsFound()
    {
        var station = CoreTestData.Station(1, new(10.2, 56.15));
        var sg = EngineTestData.BuildSpatialGrid(new Dictionary<ushort, Station> { { station.Id, station } });
        var path = new List<Position>([new(10.0, 56.15), new(10.5, 56.15)]);
        var result = sg.GetStationsAlongPolyline(path, 15);

        Assert.Contains(result, s => s == station.Id);
    }

    [Fact]
    public void GetStationsAlongPolyline_NoDuplicates_WhenStationNearMultipleSegments()
    {
        var station = CoreTestData.Station(1, new(10.2, 56.15));
        var sg = EngineTestData.BuildSpatialGrid(new Dictionary<ushort, Station> { { station.Id, station } });
        var path = new List<Position>([
            new (10.0, 56.15),
            new (10.2, 56.15),
            new (10.5, 56.15)
        ]);
        var result = sg.GetStationsAlongPolyline(path, 15);
        Assert.Single(result);
    }
}
