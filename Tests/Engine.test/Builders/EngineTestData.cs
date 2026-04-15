namespace Engine.test.Builders;

using Core.Charging.ChargingModel;
using Core.Shared;
using Core.Vehicles;
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
using Core.Charging;
using Core.test.Builders;

/// <summary>
/// Ugly file for construction of objects more easily where we do not have to specifiy all properties or think about paths.
/// Just add it here if you think anything is missing.
/// </summary>
public static class EngineTestData
{
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

    public static ConnectedEV ConnectedEV(int evId, double currentSoC, double targetSoC)
    {
        var model = EVModels.Models.First(m => m.Model == "Volkswagen ID.3");
        return new ConnectedEV(
            EVId: evId,
            CurrentSoC: currentSoC,
            TargetSoC: targetSoC,
            CapacityKWh: model.BatteryConfig.MaxCapacityKWh,
            MaxChargeRateKW: model.BatteryConfig.ChargeRateKW,
            ArrivalTime: new Time(0));
    }

    public static StationService StationService(
        Dictionary<ushort, Station> stations,
        EventScheduler scheduler,
        EVStore evStore,
        ChargingIntegrator? integrator = null)
    {
        var metrics = MetricsService();
        var actualIntegrator = integrator ?? new ChargingIntegrator(10);
        return new StationService(
            stations: [.. stations.Values],
            integrator: actualIntegrator,
            scheduler: scheduler,
            evStore: evStore,
            metrics: metrics);
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
    }

    private static Dictionary<ushort, Station> CreateAllStations()
    {
        var stationFactory = new StationFactory(
            new StationFactoryOptions(),
            _random,
            CoreTestData.EnergyPrices,
            new FileInfo(AppContext.GetData("ChargersPath") as string ?? throw new SkillissueException()));
        return stationFactory.CreateStations().ToDictionary(s => s.Id, s => s);
    }

    internal sealed class FixedEnergyPrices(float fixedPrice) : EnergyPrices(new FileInfo("data/energy_prices.csv"), new Random(42))
    {
        private readonly float _fixedPrice = fixedPrice;

        public float CalculatePrice() => _fixedPrice;
    }
}
