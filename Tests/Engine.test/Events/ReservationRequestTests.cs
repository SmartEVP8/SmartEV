namespace Testing;

using Core.Shared;
using Engine.Events;
using Engine.Routing;
using Engine.Services;
using Engine.Vehicles;
using Engine.test.Builders;
using Engine.Init;
using Engine.Cost;
using Engine.Metrics;
using Engine.StationFactory;

public class ReservationRequestTests
{
    private const ushort _stationId = 1;
    private const int _evId = 0;

    private readonly EngineSettings _engineSettings;

    public ReservationRequestTests()
    {
        _engineSettings = new EngineSettings
        {
            CostConfig = new CostWeights(),
            RunId = Guid.NewGuid(),
            MetricsConfig = new MetricsConfig(),
            Seed = new Random(42),
            StationFactoryOptions = new StationFactoryOptions(),

            CurrentAmoutOfEVsInDenmark = 1,
            IntervalToCheckUrgency = 10,
            MaxNoiseArrivalTime = 0,
            ChargingStepSeconds = 1,

            EnergyPricesPath = new FileInfo("dummy"),
            OsrmPath = new FileInfo("dummy"),
            CitiesPath = new FileInfo("dummy"),
            GridPath = new FileInfo("dummy"),
            StationsPath = new FileInfo("dummy"),
            PolygonPath = new FileInfo("dummy"),
        };
    }

    [Fact]
    public void SetsReservationAndSchedulesArrival()
    {
        var scheduler = new EventScheduler([]);
        var evStore = new EVStore(10);

        var stations = TestData.Stations((_stationId, 1.0, 1.0));

        var stationService = new StationService(
            stations: stations.Values.ToList(),
            integrator: null!,
            scheduler: scheduler,
            pathDeviator: new PathDeviator(TestData.OSRMRouter),
            random: new Random(42),
            settings: _engineSettings);

        var path = TestData.Route(0.0, 0.0, 2.0, 2.0);
        var ev = TestData.EV(path);
        evStore.Set(_evId, ref ev);

        stationService.HandleReservationRequest(
            new ReservationRequest(_evId, _stationId, new Time(0)), evStore);

        Assert.Equal(_stationId, evStore.Get(_evId).HasReservationAtStationId);
        Assert.IsType<ArriveAtStation>(scheduler.GetNextEvent());
    }

    [Fact]
    public void ExistingReservation_CancelsOldArrival()
    {
        var scheduler = new EventScheduler([]);
        var evStore = new EVStore(10);

        var stations = TestData.Stations((_stationId, 1.0, 1.0));

        var stationService = new StationService(
            stations: [.. stations.Values],
            integrator: null!,
            scheduler: scheduler,
            pathDeviator: new PathDeviator(TestData.OSRMRouter),
            random: new Random(42),
            settings: _engineSettings);

        var path = TestData.Route(0.0, 0.0, 2.0, 2.0);
        var ev = TestData.EV(path);
        evStore.Set(_evId, ref ev);

        stationService.HandleReservationRequest(
            new ReservationRequest(_evId, _stationId, new Time(0)), evStore);

        stationService.HandleReservationRequest(
            new ReservationRequest(_evId, _stationId, new Time(0)), evStore);

        Assert.IsType<ArriveAtStation>(scheduler.GetNextEvent());
        Assert.Null(scheduler.GetNextEvent());
    }
}