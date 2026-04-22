namespace Engine.Test.Events;

using Core.Shared;
using Engine.Cost;
using Engine.Events;
using Engine.Routing;
using Engine.test.Builders;
using Core.test.Builders;
using Engine.Vehicles;
using Engine.Services;
using Core.Charging;

public class FindCandidateStationsHandlerTest
{
    private readonly FindCandidateStationsHandler _handler;
    private readonly EventScheduler _eventScheduler;
    private readonly EVStore _evStore;
    private readonly IFindCandidateStationService _findCandidateStationService;
    private readonly StationService _stationService;

    public FindCandidateStationsHandlerTest()
    {
        var weights = new CostWeights();
        _eventScheduler = new EventScheduler();
        var costStore = new CostStore(weights);
        var router = EngineTestData.OSRMRouter;
        var stations = EngineTestData.AllStations;
        _evStore = new EVStore(1000);
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
            _evStore,
            evDetourPlanner,
            _stationService);
    }

    [Fact]
    public async Task Handle_NoReservationAndNoCandidates_DoesNothing()
    {
        _evStore.TryAllocate((_, ref e) => { e = CoreTestData.EV(); }, out var index1);
        var ev = _evStore.Get(index1);
        var e = new FindCandidateStations(index1, 10);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(e));
    }

    [Fact]
    public async Task Handle_NoReservationButCandidates_SchedulesNewFindcandidate()
    {
        var waypoints = new List<Position> { new(58, 9), new(58.5, 9.5) };
        var ev = CoreTestData.EV(waypoints);
        _evStore.TryAllocate((_, ref e) => { e = ev; }, out var index1);
        var e = new FindCandidateStations(index1, 0);

        var stubService = (StubFindCandidateStationService)_findCandidateStationService;
        var stationId = EngineTestData.AllStations.Keys.First();
        stubService.SetCandidates(index1, new Dictionary<ushort, (float, float)> { { stationId, (100f, 100f) } });

        await _handler.Handle(e);

        var nextEvent = _eventScheduler.GetNextEvent();
        Assert.NotNull(nextEvent);

        Assert.True(nextEvent is FindCandidateStations);

        var changedEv = _evStore.Get(index1);
        var reservedStationId = _stationService.GetReservationStationId(index1);
        Assert.Equal(stationId, reservedStationId);
    }

    [Fact]
    public async Task Handle_ExistingReservationAndNoCandidates_SchedulesArrival()
    {
        var waypoints = new List<Position> { new(58, 9), new(58.5, 9.5) };
        var ev = CoreTestData.EV(waypoints);
        var stationId = EngineTestData.AllStations.Keys.First();
        _evStore.TryAllocate((_, ref e) => { e = ev; }, out var index1);
        _stationService.HandleReservation(new Reservation(index1, 0, 0.2, 0.8), stationId);

        var e = new FindCandidateStations(index1, 0);

        await _handler.Handle(e);

        var nextEvent = _eventScheduler.GetNextEvent();
        Assert.NotNull(nextEvent);
        Assert.IsType<ArriveAtStation>(nextEvent);

        var arriveEvent = (ArriveAtStation)nextEvent;
        Assert.Equal(stationId, arriveEvent.StationId);
    }

    [Fact]
    public async Task Handle_ExistingReservationBetterThanCandidates_SchedulesNewFindCandidates()
    {
        var waypoints = new List<Position> { new(58, 9), new(58.5, 9.5) };
        var stationId = EngineTestData.AllStations.Keys.First();
        var ev = CoreTestData.EV(waypoints, originalDuration: 10000000, departureTime: 0);
        _evStore.TryAllocate((_, ref e) => { e = ev; }, out var index1);
        _stationService.HandleReservation(new Reservation(index1, 0, 0.2, 0.8), stationId);

        var e = new FindCandidateStations(index1, 0);

        var stubService = (StubFindCandidateStationService)_findCandidateStationService;
        stubService.SetCandidates(index1, new Dictionary<ushort, (float, float)> { { stationId, (1000f, 1000f) } });

        await _handler.Handle(e);

        var nextEvent = _eventScheduler.GetNextEvent();
        Assert.NotNull(nextEvent);
        Assert.True(nextEvent is FindCandidateStations);

        var changedEv = _evStore.Get(index1);
        var reservedStationId = _stationService.GetReservationStationId(index1);
        Assert.Equal(stationId, reservedStationId);
    }

    [Fact]
    public async Task Handle_ExistingReservationWorseThanCandidates_CancelEventAndScheduleFindcandidate()
    {
        var waypoints = new List<Position> { new(58, 9), new(58.5, 9.5) };
        var ev = CoreTestData.EV(waypoints);
        var existingStationId = EngineTestData.AllStations.Keys.First();
        _evStore.TryAllocate((_, ref e) => { e = ev; }, out var index1);
        _stationService.HandleReservation(new Reservation(index1, 0, 0.2, 0.8), existingStationId);

        var e = new FindCandidateStations(index1, 0);

        var stubService = (StubFindCandidateStationService)_findCandidateStationService;
        var betterStationId = EngineTestData.AllStations.Keys.Skip(1).First();
        stubService.SetCandidates(index1, new Dictionary<ushort, (float, float)> { { betterStationId, (50f, 50f) } });

        await _handler.Handle(e);
        var eventAfterCancel = _eventScheduler.GetNextEvent();
        Assert.NotNull(eventAfterCancel);
        Assert.True(eventAfterCancel is FindCandidateStations);

        var changedEv = _evStore.Get(index1);
        var reservedStationId = _stationService.GetReservationStationId(index1);
        Assert.Equal(betterStationId, reservedStationId);
    }

    [Fact]
    public async Task Handle_TooCloseToStation_SchedulesArrivalInsteadOfFindCandidate()
    {
        var waypoints = new List<Position> { new(58, 9), new(58.5, 9.5) };
        var stationId = EngineTestData.AllStations.Keys.First();
        var ev = CoreTestData.EV(waypoints, originalDuration: 500000, departureTime: 0);

        _evStore.TryAllocate((_, ref e) => { e = ev; }, out var index1);
        _stationService.HandleReservation(new Reservation(index1, 0, 0.2, 0.8), stationId);

        ref var storedEv = ref _evStore.Get(index1);
        storedEv.Advance(1);
        var e = new FindCandidateStations(index1, 1);

        var stubService = (StubFindCandidateStationService)_findCandidateStationService;
        stubService.SetCandidates(index1, new Dictionary<ushort, (float, float)> { { stationId, (1f, 1f) } });

        await _handler.Handle(e);

        var nextEvent = _eventScheduler.GetNextEvent();
        Assert.NotNull(nextEvent);
        Assert.True(nextEvent is ArriveAtStation);
    }

    private class StubFindCandidateStationService : IFindCandidateStationService
    {
        private readonly Dictionary<int, Dictionary<ushort, (float, float)>> _candidates = [];

        public void SetCandidates(int evId, Dictionary<ushort, (float, float)> candidates) => _candidates[evId] = candidates;

        public Task<Dictionary<ushort, (float, float)>> GetCandidateStationFromCache(int evId) => Task.FromResult(_candidates.TryGetValue(evId, out var c) ? c : []);

        public Action<IMiddlewareEvent> PreComputeCandidateStation() => _ => { };
    }
}
