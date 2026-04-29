namespace Engine.Test.Events;

using Engine.Events.Middleware;
using Core.Shared;
using Engine.Cost;
using Engine.Events;
using Engine.Routing;
using Engine.test.Builders;
using Core.test.Builders;
using Engine.Services;
using Core.Charging;
using Core.Vehicles;

public class FindCandidateStationsHandlerTest
{
    private readonly FindCandidateStationsHandler _handler;
    private readonly EventScheduler _eventScheduler;
    private readonly Dictionary<int, EV> _evStore;
    private readonly IFindCandidateStationService _findCandidateStationService;
    private readonly StationService _stationService;

    public FindCandidateStationsHandlerTest()
    {
        var weights = new CostWeights();
        _eventScheduler = new EventScheduler();
        var costStore = new CostStore(weights);
        var router = EngineTestData.OSRMRouter;
        var stations = EngineTestData.AllStations;
        _evStore = new Dictionary<int, EV>();
        var costFunction = new CostFunction(
            costStore,
            EngineTestData.StationService(stations, _eventScheduler, _evStore),
            CoreTestData.EnergyPrices);
        var evDetourPlanner = new EVDetourPlanner(router);
        _stationService = EngineTestData.StationService(stations, _eventScheduler, _evStore);

        _findCandidateStationService = new StubFindCandidateStationService();

        _handler = new FindCandidateStationsHandler(
            _findCandidateStationService,
            costFunction,
            _eventScheduler,
            evDetourPlanner,
            _stationService);
    }

    [Fact]
    public async Task Handle_NoReservationAndNoCandidates_DoesNothing()
    {
        var ev = CoreTestData.EV();
        _evStore[ev.Id] = ev;
        var e = new FindCandidateStations(ev, 10);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(e));
    }

    [Fact]
    public async Task Handle_NoReservationButCandidates_SchedulesNewFindcandidate()
    {
        var waypoints = new List<Position> { new(58, 9), new(58.5, 9.5) };
        var ev = CoreTestData.EV(waypoints, originalDuration: 10000000);
        _evStore[ev.Id] = ev;
        var e = new FindCandidateStations(ev, 0);

        var stubService = (StubFindCandidateStationService)_findCandidateStationService;
        var stationId = EngineTestData.AllStations.Keys.First();
        stubService.SetCandidates(ev.Id, new Dictionary<ushort, DurToStationAndDest> { { stationId, new DurToStationAndDest(350000f, 350000f, 300_000f, 500f) } });

        await _handler.Handle(e);

        var nextEvent = _eventScheduler.GetNextEvent();
        Assert.NotNull(nextEvent);
        Assert.True(nextEvent is FindCandidateStations);

        var reservedStationId = _stationService.GetReservationStationId(ev.Id);
        Assert.Equal(stationId, reservedStationId);
    }

    [Fact]
    public async Task Handle_ExistingReservationAndNoCandidates_SchedulesArrival()
    {
        var waypoints = new List<Position> { new(58, 9), new(58.5, 9.5) };
        var ev = CoreTestData.EV(waypoints);
        var stationId = EngineTestData.AllStations.Keys.First();
        _evStore[ev.Id] = ev;
        _stationService.HandleReservation(new Reservation(ev.Id, 0, 0.2, 0.8), stationId);

        var e = new FindCandidateStations(ev, 0);

        await _handler.Handle(e);

        var nextEvent = _eventScheduler.GetNextEvent();
        Assert.NotNull(nextEvent);
        Assert.IsType<ArriveAtStation>(nextEvent);

        var arriveEvent = (ArriveAtStation)nextEvent;
        Assert.Equal(stationId, arriveEvent.Station.Id);
    }

    [Fact]
    public async Task Handle_ExistingReservationBetterThanCandidates_SchedulesNewFindCandidates()
    {
        var waypoints = new List<Position> { new(58, 9), new(58.5, 9.5) };
        var stationId = EngineTestData.AllStations.Keys.First();
        var ev = CoreTestData.EV(waypoints, originalDuration: 10000000, departureTime: 0);
        _evStore[ev.Id] = ev;
        _stationService.HandleReservation(new Reservation(ev.Id, 0, 0.1, 0.5), stationId);

        var e = new FindCandidateStations(ev, 0);

        var stubService = (StubFindCandidateStationService)_findCandidateStationService;
        stubService.SetCandidates(ev.Id, new Dictionary<ushort, DurToStationAndDest> { { stationId, new DurToStationAndDest(1000f, 1000f, 300_000f, 500f) } });

        await _handler.Handle(e);

        var nextEvent = _eventScheduler.GetNextEvent();
        Assert.NotNull(nextEvent);
        Assert.True(nextEvent is FindCandidateStations);

        var reservedStationId = _stationService.GetReservationStationId(ev.Id);
        Assert.Equal(stationId, reservedStationId);
    }

    [Fact]
    public async Task Handle_ExistingReservationWorseThanCandidates_CancelEventAndScheduleFindcandidate()
    {
        var waypoints = new List<Position> { new(58, 9), new(58.5, 9.5) };
        var ev = CoreTestData.EV(waypoints, originalDuration: 10000000);
        var existingStationId = EngineTestData.AllStations.Keys.First();
        _evStore[ev.Id] = ev;
        _stationService.HandleReservation(new Reservation(ev.Id, 0, 0.1, 0.5), existingStationId);

        var e = new FindCandidateStations(ev, 0);

        var stubService = (StubFindCandidateStationService)_findCandidateStationService;
        var betterStationId = EngineTestData.AllStations.Keys.Skip(1).First();
        stubService.SetCandidates(ev.Id, new Dictionary<ushort, DurToStationAndDest> { { betterStationId, new DurToStationAndDest(350000f, 350000f, 300_000f, 500f) } });

        await _handler.Handle(e);

        var eventAfterCancel = _eventScheduler.GetNextEvent();
        Assert.NotNull(eventAfterCancel);
        Assert.True(eventAfterCancel is FindCandidateStations);

        var reservedStationId = _stationService.GetReservationStationId(ev.Id);
        Assert.Equal(betterStationId, reservedStationId);
    }

    [Fact]
    public async Task Handle_TooCloseToStation_SchedulesArrivalInsteadOfFindCandidate()
    {
        var waypoints = new List<Position> { new(58, 9), new(58.5, 9.5) };
        var stationId = EngineTestData.AllStations.Keys.First();
        var ev = CoreTestData.EV(waypoints, originalDuration: 500000, departureTime: 0);
        _evStore[ev.Id] = ev;
        _stationService.HandleReservation(new Reservation(ev.Id, 0, 0.1, 0.5), stationId);

        var storedEv = _evStore[ev.Id];
        storedEv.Advance(1);
        _evStore[ev.Id] = storedEv;
        var e = new FindCandidateStations(storedEv, 1);

        var stubService = (StubFindCandidateStationService)_findCandidateStationService;
        stubService.SetCandidates(ev.Id, new Dictionary<ushort, DurToStationAndDest> { { stationId, new DurToStationAndDest(1f, 1f, 300_000f, 500f) } });

        await _handler.Handle(e);

        var nextEvent = _eventScheduler.GetNextEvent();
        Assert.NotNull(nextEvent);
        Assert.True(nextEvent is ArriveAtStation);
    }

    private class StubFindCandidateStationService : IFindCandidateStationService
    {
        private readonly Dictionary<int, Dictionary<ushort, DurToStationAndDest>> _candidates = [];

        public void SetCandidates(int evId, Dictionary<ushort, DurToStationAndDest> candidates) => _candidates[evId] = candidates;

        public Task<Dictionary<ushort, DurToStationAndDest>> GetCandidateStationFromCache(int evId) => Task.FromResult(_candidates.TryGetValue(evId, out var c) ? c : []);

        public Action<IMiddlewareEvent> PreComputeCandidateStation() => _ => { };
    }
}
