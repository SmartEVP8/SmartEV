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

/// <summary>
/// Ugly file for construction of objects more easily where we do not have to specifiy all properties or think about paths.
/// Just add it here if you think anything is missing.
/// </summary>
public static class TestData
{
    private static readonly Random _random = new(1);

    public static readonly EnergyPrices EnergyPrices =
        new(new FileInfo(AppContext.GetData("EnergyPricesPath") as string
            ?? throw new InvalidDataException("EnergyPricesPath not set.")));

    private static readonly StationFactory _stationFactory = new(
        new StationFactoryOptions(),
        _random,
        EnergyPrices,
        new FileInfo(AppContext.GetData("ChargersPath") as string ?? throw new SkillissueException()));

    public static readonly Dictionary<ushort, Station> AllStations = _stationFactory.CreateStations();

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

    public static Station Station(ushort id, Position pos, List<ChargerBase>? chargers = null) =>
        new(id, string.Empty, string.Empty, pos, chargers ?? [], _random, EnergyPrices);

    public static Dictionary<ushort, Station> Stations(params (ushort Id, double Lon, double Lat)[] stations) =>
        stations.ToDictionary(s => s.Id, s => Station(s.Id, new Position(s.Lon, s.Lat)));

    public static SpatialGrid BuildSpatialGrid(Dictionary<ushort, Station>? stations = null)
    {
        var gridPath = AppContext.GetData("GridPath") as string
            ?? throw new InvalidOperationException("GridPath not set.");
        var polygons = PolygonParser.Parse(File.ReadAllText(gridPath));
        var grid = Polygooner.GenerateGrid(0.1, polygons);
        return new SpatialGrid(grid, stations ?? []);
    }

    public static Paths Route(double fromLon, double fromLat, double toLon, double toLat)
    {
        var (_, polyline) = OSRMRouter.QuerySingleDestination(fromLon, fromLat, toLon, toLat);
        return Polyline6ToPoints.DecodePolyline(polyline);
    }

    public static Journey Journey(List<Position>? waypoints, Time departure = default, Time originalDuration = default)
    {
        if (waypoints == null)
        {
            return new(departure, originalDuration, new Paths([new Position(0, 0), new Position(1, 1)]));
        }

        return new(departure, originalDuration, new Paths(waypoints));
    }

    public static Battery Battery(
        ushort capacity = 100,
        ushort maxChargeRate = 150,
        float stateOfCharge = 80f,
        Socket socket = Socket.CCS2) =>
        new(capacity, maxChargeRate, stateOfCharge, socket);

    public static Preferences Preferences(
        float PriceSensitivity = 1f,
        float MinAcceptableCharge = 0.1f,
        float MaxPathDeviation = 10.0f) =>
        new(PriceSensitivity, MinAcceptableCharge, MaxPathDeviation);

    public static EV EV(
        List<Position>? waypoints = null,
        Battery? battery = null,
        Preferences? preferences = null,
        ushort efficiency = 150) =>
        new(
            battery ?? Battery(),
            preferences ?? Preferences(),
            Journey(waypoints),
            efficiency);

    public static EV EV() =>
        new(
            Battery(),
            Preferences(),
            Journey(null),
            150);
}
