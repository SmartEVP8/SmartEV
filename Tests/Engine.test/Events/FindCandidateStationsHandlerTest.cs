namespace Engine.Test.Events;

using Core.Shared;
using Engine.Cost;
using Engine.Events;
using Engine.Routing;
using Engine.test.Builders;
using Engine.Vehicles;

public class FindCandidateStationsHandlerTest
{
    private readonly FindCandidateStationsHandler _handler;
    private readonly EventScheduler _eventScheduler;
    private readonly EVStore _evStore;
    private readonly IFindCandidateStationService _findCandidateStationService;

    // Stub for candidate station service
    private class StubFindCandidateStationService : IFindCandidateStationService
    {
        private readonly Dictionary<int, Dictionary<ushort, float>> _candidates = [];

        public void SetCandidates(int evId, Dictionary<ushort, float> candidates) => _candidates[evId] = candidates;

        public Task<Dictionary<ushort, float>> GetCandidateStationFromCache(int evId) => Task.FromResult(_candidates.TryGetValue(evId, out var c) ? c : []);

        public Action<IMiddlewareEvent> PreComputeCandidateStation() => _ => { };
    }

    public FindCandidateStationsHandlerTest()
    {
        var weights = new CostWeights();
        _eventScheduler = new EventScheduler();
        var costStore = new CostStore(weights);
        var router = TestData.OSRMRouter;
        var stations = TestData.AllStations;
        _evStore = new EVStore(1000);
        var costFunction = new CostFunction(
            costStore,
            TestData.StationService(stations, _eventScheduler, _evStore),
            TestData.EnergyPrices);
        var evDetourPlanner = new EVDetourPlanner(router);

        _findCandidateStationService = new StubFindCandidateStationService();

        _handler = new FindCandidateStationsHandler(
            _findCandidateStationService,
            costFunction,
            _eventScheduler,
            _evStore,
            evDetourPlanner);
    }

    [Fact]
    public async Task Handle_NoReservationAndNoCandidates_DoesNothing()
    {
        _evStore.TryAllocate((_, ref e) => { e = TestData.EV(); }, out var index1);
        var ev = _evStore.Get(index1);
        var e = new FindCandidateStations(index1, 10);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(e));
    }

    [Fact]
    public async Task Handle_NoReservationButCandidates_SchedulesNewFindcandidate()
    {
        var waypoints = new List<Position> { new Position(58, 9), new Position(58.5, 9.5) };
        var ev = TestData.EV(waypoints);
        _evStore.TryAllocate((_, ref e) => { e = ev; }, out var index1);
        var e = new FindCandidateStations(index1, 0);

        var stubService = (StubFindCandidateStationService)_findCandidateStationService;
        var stationId = TestData.AllStations.Keys.First();
        stubService.SetCandidates(index1, new Dictionary<ushort, float> { { stationId, 100f } });

        await _handler.Handle(e);

        var nextEvent = _eventScheduler.GetNextEvent();
        Assert.NotNull(nextEvent);

        Assert.True(nextEvent is FindCandidateStations);

        var changedEv = _evStore.Get(index1);
        Assert.Equal(stationId, changedEv.HasReservationAtStationId);
    }

    [Fact]
    public async Task Handle_ExistingReservationAndNoCandidates_SchedulesArrival()
    {
        var waypoints = new List<Position> { new Position(58, 9), new Position(58.5, 9.5) };
        var ev = TestData.EV(waypoints);
        var stationId = TestData.AllStations.Keys.First();
        ev.HasReservationAtStationId = stationId; // Existing reservation
        _evStore.TryAllocate((_, ref e) => { e = ev; }, out var index1);
        var e = new FindCandidateStations(index1, 0);

        // Act
        await _handler.Handle(e);

        // Assert
        var nextEvent = _eventScheduler.GetNextEvent();
        Assert.NotNull(nextEvent);
        Assert.IsType<ArriveAtStation>(nextEvent);

        var arriveEvent = (ArriveAtStation)nextEvent;
        Assert.Equal(stationId, arriveEvent.StationId);
    }

    [Fact]
    public async Task Handle_ExistingReservationBetterThanCandidates_SchedulesNewFindCandidates()
    {
        var waypoints = new List<Position> { new Position(58, 9), new Position(58.5, 9.5) };
        var stationId = TestData.AllStations.Keys.First();
        var ev = TestData.EV(waypoints);
        ev.HasReservationAtStationId = stationId;
        _evStore.TryAllocate((_, ref e) => { e = ev; }, out var index1);
        var e = new FindCandidateStations(index1, 0);

        var stubService = (StubFindCandidateStationService)_findCandidateStationService;

        stubService.SetCandidates(index1, new Dictionary<ushort, float> { { stationId, 100f } });

        await _handler.Handle(e);

        var nextEvent = _eventScheduler.GetNextEvent();
        Assert.NotNull(nextEvent);
        Assert.True(nextEvent is FindCandidateStations);

        var changedEv = _evStore.Get(index1);
        Assert.Equal(stationId, changedEv.HasReservationAtStationId);
    }

    [Fact]
    public async Task Handle_ExistingReservationWorseThanCandidates_CancelEventAndScheduleFindcandidate()
    {
        var waypoints = new List<Position> { new Position(58, 9), new Position(58.5, 9.5) };
        var ev = TestData.EV(waypoints);
        var existingStationId = TestData.AllStations.Keys.First();
        ev.HasReservationAtStationId = existingStationId;
        _evStore.TryAllocate((_, ref e) => { e = ev; }, out var index1);
        var e = new FindCandidateStations(index1, 0);

        var stubService = (StubFindCandidateStationService)_findCandidateStationService;
        var betterStationId = TestData.AllStations.Keys.Skip(1).First();
        stubService.SetCandidates(index1, new Dictionary<ushort, float> { { betterStationId, 50f } });

        await _handler.Handle(e);

        var nextEvent = _eventScheduler.GetNextEvent();
        Assert.NotNull(nextEvent);

        Assert.True(nextEvent is CancelRequest);

        var eventAfterCancel = _eventScheduler.GetNextEvent();
        Assert.NotNull(eventAfterCancel);
        Assert.True(eventAfterCancel is FindCandidateStations);
        var changedEv = _evStore.Get(index1);
        Assert.Equal(betterStationId, changedEv.HasReservationAtStationId);
    }

    [Fact]
    public async Task Handle_TooCloseToStation_SchedulesArrivalInsteadOfFindCandidate()
    {
        var waypoints = new List<Position> { new Position(58, 9), new Position(58.5, 9.5) };
        var ev = TestData.EV(waypoints);
        _evStore.TryAllocate((_, ref e) => { e = ev; }, out var index1);
        var e = new FindCandidateStations(index1, 0);

        var stubService = (StubFindCandidateStationService)_findCandidateStationService;
        var stationId = TestData.AllStations.Keys.First();
        stubService.SetCandidates(index1, new Dictionary<ushort, float> { { stationId, 1f } });

        await _handler.Handle(e);

        var nextEvent = _eventScheduler.GetNextEvent();
        Assert.NotNull(nextEvent);
        Assert.IsType<ArriveAtStation>(nextEvent);

        var arriveEvent = (ArriveAtStation)nextEvent;
        Assert.Equal(stationId, arriveEvent.StationId);
    }
}
