namespace Engine.test.Grid;

using Core.Charging;
using Core.Shared;
using Engine.Grid;
using Engine.Parsers;

public class SpatialGridTests
{
    private static readonly Random _random = new();
    private readonly EnergyPrices _energyPrices = new(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "energy_prices.csv")));

    public SpatialGrid BuildSpatialGrid(IEnumerable<Station> stations)
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
        var station1 = new Station(1, string.Empty, string.Empty, new Position(10.0, 56.0), null, _random, _energyPrices);
        var station2 = new Station(2, string.Empty, string.Empty, new Position(10.5, 56.5), null, _random, _energyPrices);
        var station3 = new Station(3, string.Empty, string.Empty, new Position(10.3, 56.5), null, _random, _energyPrices);

        var sg = BuildSpatialGrid([station1, station2, station3]);

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
        var nearby = new Station(1, string.Empty, string.Empty, new Position(10.2, 56.15), null, _random, _energyPrices);
        var farAway = new Station(2, string.Empty, string.Empty, new Position(12.5, 55.6), null, _random, _energyPrices);
        var sg = BuildSpatialGrid([nearby, farAway]);
        var path = new Paths([new Position(10.0, 56.15), new Position(10.5, 56.15)]);
        var result = sg.GetStationsAlongPolyline(path, 15);
        Assert.Contains(result, s => s == nearby.Id);
        Assert.DoesNotContain(result, s => s == farAway.Id);
    }

    [Fact]
    public void GetStationsAlongPolyline_StationPerpendicularToSegment_IsFound()
    {
        var station = new Station(1, string.Empty, string.Empty, new Position(10.2, 56.15), null, _random, _energyPrices);
        var sg = BuildSpatialGrid([station]);
        var path = new Paths([new Position(10.0, 56.15), new Position(10.5, 56.15)]);
        var result = sg.GetStationsAlongPolyline(path, 15);
        Assert.Contains(result, s => s == station.Id);
    }

    [Fact]
    public void GetStationsAlongPolyline_NoDuplicates_WhenStationNearMultipleSegments()
    {
        var station = new Station(1, string.Empty, string.Empty, new Position(10.2, 56.15), null, _random, _energyPrices);
        var sg = BuildSpatialGrid([station]);
        var path = new Paths([
            new Position(10.0, 56.15),
            new Position(10.2, 56.15),
            new Position(10.5, 56.15)
        ]);
        var result = sg.GetStationsAlongPolyline(path, 15);
        Assert.Single(result);
    }
}
