namespace Engine.test.Builders;

using System.Collections.Immutable;
using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Charging.ChargingModel.Chargepoint;
using Core.Shared;
using Core.Vehicles;
using Core.Routing;
using Engine.Routing;
using Engine.Grid;
using Engine.Parsers;
using Engine.Utils;
using Engine.StationFactory;
using Engine.Cost;
using Engine.Metrics;
using Engine.Vehicles;
using Engine.Events;
using Engine.Services;
using Engine.Spawning;
using Engine.Init;

/// <summary>
/// Ugly file for construction of objects more easily where we do not have to specifiy all properties or think about paths.
/// Just add it here if you think anything is missing.
/// </summary>
public static class TestData
{
    public static readonly EnergyPrices EnergyPrices =
        new(new FileInfo(AppContext.GetData("EnergyPricesPath") as string
            ?? throw new InvalidDataException("EnergyPricesPath not set.")), new Random(1));

    private static readonly Random _random = new(1);

    private static Dictionary<ushort, Station>? _allStations;

    public static Dictionary<ushort, Station> AllStations => _allStations ??= CreateAllStations();

    public static OSRMRouter OSRMRouter => new(
                            new FileInfo(AppContext.GetData("OsrmDataPath") as string
                                    ?? throw new InvalidDataException("OSRMPath not set")), [.. AllStations.Values]);

    public static readonly SpatialGrid SpatialGrid = BuildSpatialGrid(AllStations);

    public static JourneySamplerProvider JourneySamplerProvider(
    float populationScalar = 1.0f,
    float distanceScalar = 1.0f) => new(JourneySamplers, populationScalar, distanceScalar);

    public static readonly JourneyPipeline JourneySamplers = BuildJourneyPipeline();

    private static JourneyPipeline BuildJourneyPipeline()
    {
        var polygonPath = AppContext.GetData("GridPath") as string
                    ?? throw new InvalidOperationException("GridPath not set in project.");

        var polygons = PolygonParser.Parse(File.ReadAllText(polygonPath));
        var spawnGrid = Polygooner.GenerateGrid(size: 0.1, polygons);

        var cityPath = AppContext.GetData("CityDataPath") as string
                            ?? throw new InvalidOperationException("GridPath not set in project.");

        var cities = InitCities(new FileInfo(cityPath));

        return new JourneyPipeline(spawnGrid, cities, OSRMRouter);
    }

    private static List<City> InitCities(FileInfo citiesPath)
    {
        return [.. File.ReadAllLines(citiesPath.ToString()).Skip(1).Select(line =>
        {
            var parts = line.Split(',');
            var name = parts[0];
            var longitude = double.Parse(parts[2]);
            var latitude = double.Parse(parts[3]);
            var population = int.Parse(parts[1]);
            return new City(name, new Position(longitude, latitude), population);
        })];
    }

    public static MetricsService MetricsService()
    {
        var config = new MetricsConfig(); // Default config
        return new MetricsService(config, Guid.NewGuid());
    }

    public static SnapshotEventHandler SnapshotHandler(
        StationService stationService,
        MetricsService metrics,
        EventScheduler scheduler) =>
        new(
            rescheduleTime: new Time(3600),
            stationService: stationService,
            metrics: metrics,
            scheduler: scheduler);

    public static Station Station(
        ushort id,
        Position? pos = null,
        EnergyPrices? energyPrices = null,
        int? queueSize = null,
        List<ChargerBase>? chargers = null)
    {
        var position = pos ?? new Position(0, 0);
        var prices = energyPrices ?? EnergyPrices;

        List<ChargerBase> chargerList;
        if (chargers != null)
            chargerList = chargers;
        else if (queueSize.HasValue)
            chargerList = [CreateFakeChargerWithQueue(queueSize.Value)];
        else
            chargerList = [CreateFakeChargerWithQueue(0)]; // Default: one empty charger

        return new Station(id, string.Empty, string.Empty, position, chargerList, prices);
    }

    public static Dictionary<ushort, Station> Stations(params (ushort Id, double Lon, double Lat)[] stations)
        => stations.ToDictionary(s => s.Id, s => Station(s.Id, new Position(s.Lon, s.Lat)));

    public static SpatialGrid BuildSpatialGrid(Dictionary<ushort, Station>? stations = null)
    {
        var gridPath = AppContext.GetData("GridPath") as string
            ?? throw new InvalidOperationException("GridPath not set.");
        var polygons = PolygonParser.Parse(File.ReadAllText(gridPath));
        var grid = Polygooner.GenerateGrid(0.1, polygons);
        return new SpatialGrid(grid, stations ?? []);
    }

    public static List<Position> Route(double fromLon, double fromLat, double toLon, double toLat)
    {
        var result = OSRMRouter.QuerySingleDestination(fromLon, fromLat, toLon, toLat);
        return Polyline6ToPoints.DecodePolyline(result.Polyline);
    }

    public static Journey Journey(List<Position>? waypoints, Time departure = default, Time originalDuration = default)
    {
        if (waypoints == null)
        {
            return new(departure, originalDuration, 100, new List<Position>([new(0, 0), new(1, 1)]));
        }

        return new(departure, originalDuration, 100, [.. waypoints]);
    }

    public static Battery Battery(
        ushort capacity = 100,
        ushort maxChargeRate = 150,
        float stateOfCharge = 0.2f,
        Socket socket = Socket.CCS2) => new(capacity, maxChargeRate, stateOfCharge, socket);

    public static Preferences Preferences(
        float PriceSensitivity = 1f,
        float MinAcceptableCharge = 0.1f,
        float MaxPathDeviation = 10.0f) => new(PriceSensitivity, MinAcceptableCharge, MaxPathDeviation);

    public static EV EV(
        List<Position>? waypoints = null,
        Battery? battery = null,
        Preferences? preferences = null,
        ushort efficiency = 150,
        uint originalDuration = 100u,
        Time departureTime = default)
    {
        return new(
            battery ?? Battery(),
            preferences ?? Preferences(),
            Journey(waypoints, originalDuration: originalDuration, departure: departureTime),
            efficiency);
    }

    public static SingleCharger SingleCharger(int id, int maxPowerKW = 150)
    {
        var connectors = new Connectors([new Connector(Socket.CCS2)]);
        var point = new SingleChargingPoint(connectors);
        return new SingleCharger(id, maxPowerKW, point);
    }

    public static DualCharger DualCharger(int id, int maxPowerKW = 150)
    {
        var point = new DualChargingPoint(new Connectors([new Connector(Socket.CCS2)]));
        return new DualCharger(id, maxPowerKW, point);
    }

    public static ConnectedEV ConnectedEV(int evId, double currentSoC, double targetSoC, Socket socket = Socket.CCS2)
    {
        var model = EVModels.Models.First(m => m.Model == "Volkswagen ID.3");
        return new ConnectedEV(
            EVId: evId,
            CurrentSoC: currentSoC,
            TargetSoC: targetSoC,
            CapacityKWh: model.BatteryConfig.MaxCapacityKWh,
            MaxChargeRateKW: model.BatteryConfig.ChargeRateKW,
            Socket: socket,
            ArrivalTime: new Time(0));
    }

    public static StationService StationService(
        Dictionary<ushort, Station> stations,
        EventScheduler scheduler,
        EVStore evStore,
        EngineSettings settings,
        ChargingIntegrator? integrator = null)
    {
        var metrics = MetricsService();
        var actualIntegrator = integrator ?? new ChargingIntegrator(10);

        return new StationService(
            stations: [.. stations.Values],
            integrator: actualIntegrator,
            scheduler: scheduler,
            evStore: evStore,
            applyNewPath: new ApplyNewPath(OSRMRouter),
            metrics: metrics,
            settings: settings);
    }
    public static EngineSettings DefaultSettings() => new()
    {
        // Snapshot-specific settings
        SnapshotInterval = 3600,
        ChargingStepSeconds = 10,

        // Config objects
        CostConfig = new(),
        MetricsConfig = new(),
        StationFactoryOptions = new(),

        // Metadata/Simulation Control
        RunId = Guid.NewGuid(),
        Seed = new Random(42),
        SimulationEndTime = 86400,

        // EV Logic
        CurrentAmoutOfEVsInDenmark = 1000,
        IntervalToUpdateEVs = 60,
        BatteryIntervalForCheckUrgency = 300,
        EVDistributionWindowsSize = 3600,
        EVSpawnFraction = 1.0,

        // Paths (Use dummy strings or actual constants if needed)
        EnergyPricesPath = new FileInfo("data/energy_prices.csv"),
        OsrmPath = new FileInfo("data/osrm.json"),
        CitiesPath = new FileInfo("data/cities.csv"),
        GridPath = new FileInfo("data/grid.csv"),
        StationsPath = new FileInfo("data/stations.csv"),
        PolygonPath = new FileInfo("data/polygon.geojson"),
    };

    internal sealed class FixedEnergyPrices(float fixedPrice) : EnergyPrices(new FileInfo("data/energy_prices.csv"), new Random(42))
    {
        private readonly float _fixedPrice = fixedPrice;

        public float CalculatePrice() => _fixedPrice;
    }

    internal sealed class StubCostStore(CostWeights weights) : ICostStore
    {
        private readonly CostWeights _weights = weights;

        public CostWeights GetWeights() => _weights;

        public void TrySet(CostWeights update, long seq)
        {
        }
    }

    internal sealed class StubStationService(Dictionary<ushort, Station> stations) : IStationService
    {
        private readonly Dictionary<ushort, Station> _stations = stations;

        public Station GetStation(ushort stationId)
        {
            return _stations.TryGetValue(stationId, out var station)
            ? station
            : throw new KeyNotFoundException($"Station {stationId} not found.");
        }

        public int GetTotalQueueSize(ushort stationId)
        {
            return _stations.TryGetValue(stationId, out var station)
            ? station.Chargers.Sum(c => c.Queue.Count)
            : throw new KeyNotFoundException($"Station {stationId} not found.");
        }
    }

    private sealed class FakeCharger() : ChargerBase(id: 1, maxPowerKW: 100)
    {
        public override ImmutableArray<Socket> GetSockets() => [Socket.CCS2];
    }

    private static Dictionary<ushort, Station> CreateAllStations()
    {
        var stationFactory = new StationFactory(
            new StationFactoryOptions(),
            _random,
            EnergyPrices,
            new FileInfo(AppContext.GetData("ChargersPath") as string ?? throw new SkillissueException()));
        return stationFactory.CreateStations().ToDictionary(s => s.Id, s => s);
    }

    private static ChargerBase CreateFakeChargerWithQueue(int queueSize)
    {
        var charger = new FakeCharger();
        for (var i = 0; i < queueSize; i++)
        {
            charger.Queue.Enqueue(i);
        }

        return charger;
    }
}
