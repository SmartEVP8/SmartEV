namespace Engine.test.Builders;

using Core.Charging;
using Core.Shared;
using Core.Vehicles;
using Core.Routing;
using Engine.Routing;
using Engine.Grid;
using Engine.Parsers;
using Engine.Utils;
using Engine.StationFactory;

public static class TestData
{
    public static readonly EnergyPrices EnergyPrices =
        new(new FileInfo(AppContext.GetData("EnergyPricesPath") as string
            ?? throw new InvalidDataException("EnergyPricesPath not set.")));

#pragma warning disable SA1202
    private static readonly Random _random = new(1);

    private static readonly StationFactory _stationFactory = new(
        new StationFactoryOptions(),
        _random,
        EnergyPrices,
        new FileInfo(AppContext.GetData("ChargersPath") as string ?? throw new SkillissueException()));

    public static readonly Dictionary<ushort, Station> AllStations = _stationFactory.CreateStations();

#pragma warning restore SA1202

    public static OSRMRouter OSRMRouter
    {
        get
        {
            var router = new OSRMRouter(new FileInfo(AppContext.GetData("OsrmDataPath") as string
                        ?? throw new InvalidDataException("OSRMPath not set")));
            router.InitStations([.. AllStations.Values]);
            return router;
        }
    }

    public static readonly SpatialGrid SpatialGrid = BuildSpatialGrid(AllStations);

    public static Paths Route(double fromLon, double fromLat, double toLon, double toLat)
    {
        var (_, polyline) = OSRMRouter.QuerySingleDestination(fromLon, fromLat, toLon, toLat);
        return Polyline6ToPoints.DecodePolyline(polyline);
    }

    public static Journey Journey(List<Position> waypoints, Time departure = default, Time originalDuration = default) =>
        new(departure, originalDuration, new Paths(waypoints));

    public static EV EV(
        Paths path,
        ushort capacity = 100,
        ushort maxChargeRate = 150,
        float stateOfCharge = 80f,
        Socket socket = Socket.CCS2,
        ushort efficiency = 150) =>
        new(new Battery(capacity, maxChargeRate, stateOfCharge, socket),
            new Preferences(1f, 0.1f, 10.0f),
            new Journey(default, default, path),
            efficiency);

    public static Station Station(ushort id, Position pos, List<ChargerBase>? chargers = null) =>
        new(id, string.Empty, string.Empty, pos, chargers ?? [], _random, EnergyPrices);

    public static Dictionary<ushort, Station> Stations(params (ushort Id, double Lon, double Lat)[] stations) =>
        stations.ToDictionary(s => s.Id, s => Station(s.Id, new Position(s.Lon, s.Lat)));

    private static SpatialGrid BuildSpatialGrid(Dictionary<ushort, Station>? stations = null)
    {
        var gridPath = AppContext.GetData("GridPath") as string
            ?? throw new InvalidOperationException("GridPath not set.");
        var polygons = PolygonParser.Parse(File.ReadAllText(gridPath));
        var grid = Polygooner.GenerateGrid(0.1, polygons);
        return new SpatialGrid(grid, stations ?? []);
    }
}
