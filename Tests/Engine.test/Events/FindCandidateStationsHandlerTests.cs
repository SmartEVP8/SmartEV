namespace Engine.test.Events;

using Core.Charging;
using Core.Shared;
using Core.Vehicles;
using Engine.Cost;
using Engine.Events;
using Engine.Events.Middleware;
using Engine.Routing;
using Engine.test.Builders;
using Engine.Vehicles;

public class FindCandidateStationsHandlerTests
{
    private readonly EVStore _evStore;
    private readonly FakeFindCandidateStationService _fakeCandidateService;
    private readonly FakeComputeCost _fakeComputeCost;
    private readonly FakeEventScheduler _fakeEventScheduler;
    private readonly FindCandidateStationsHandler _handler;

    public FindCandidateStationsHandlerTests()
    {
        _evStore = new EVStore(1);
        _fakeCandidateService = new FakeFindCandidateStationService();
        _fakeComputeCost = new FakeComputeCost();
        _fakeEventScheduler = new FakeEventScheduler();
        _handler = new FindCandidateStationsHandler(
            _fakeCandidateService,
            _fakeComputeCost,
            _fakeEventScheduler,
            _evStore,
            new FakeApplyNewPath());
    }

    [Fact]
    public async Task Case1_NoReservation_SchedulesArriveAtStationAndNextDecision()
    {
        _evStore.TryAllocate((_, ref ev) => ev = TestData.EV(originalDuration: 100, departureTime: new Time(0)), out var evId);
        ref var ev = ref _evStore.Get(evId);
        ev.HasReservationAtStationId = null;

        var station = TestData.Station(id: 1);
        _fakeCandidateService.Result = new Dictionary<ushort, float> { { 1, 10f } };
        _fakeComputeCost.Result = station;

        await _handler.Handle(new FindCandidateStations(evId, Time: 0));

        Assert.Contains(_fakeEventScheduler.ScheduledEvents, e => e is ArriveAtStation);
        Assert.Contains(_fakeEventScheduler.ScheduledEvents, e => e is FindCandidateStations);
    }

    [Fact]
    public async Task Case2_ReservationMatchesBestStation_NoNewEventsScheduled()
    {
        _evStore.TryAllocate((_, ref ev) => ev = TestData.EV(originalDuration: 100, departureTime: new Time(0)), out var evId);
        ref var ev = ref _evStore.Get(evId);
        var station = TestData.Station(id: 1);
        ev.HasReservationAtStationId = station.Id;

        _fakeCandidateService.Result = new Dictionary<ushort, float> { { 1, 10f } };
        _fakeComputeCost.Result = station;

        await _handler.Handle(new FindCandidateStations(evId, Time: 0));

        Assert.Empty(_fakeEventScheduler.ScheduledEvents);
    }

    [Fact]
    public async Task Case3_ReservationMismatch_SchedulesCancelAndReschedules()
    {
        _evStore.TryAllocate((_, ref ev) => ev = TestData.EV(originalDuration: 100, departureTime: new Time(0)), out var evId);
        ref var ev = ref _evStore.Get(evId);
        ev.HasReservationAtStationId = 99;

        var station = TestData.Station(id: 1);
        _fakeCandidateService.Result = new Dictionary<ushort, float> { { 1, 10f } };
        _fakeComputeCost.Result = station;

        await _handler.Handle(new FindCandidateStations(evId, Time: 0));

        Assert.Contains(_fakeEventScheduler.ScheduledEvents, e => e is CancelRequest);
        Assert.Contains(_fakeEventScheduler.ScheduledEvents, e => e is ArriveAtStation);
        Assert.Contains(_fakeEventScheduler.ScheduledEvents, e => e is FindCandidateStations);
    }

    // TODO: Figure out if these should be shared between files or per file.
    public class FakeEventScheduler : IEventScheduler
    {
        public List<Event> ScheduledEvents { get; } = [];

        public uint ScheduleEvent(Event e)
        {
            ScheduledEvents.Add(e);
            return 0;
        }
    }

    public class FakeComputeCost : IComputeCost
    {
        public Station? Result { get; set; }

        public Station Compute(ref EV ev, Dictionary<ushort, float> stationDurations, Time time) => Result!;
    }

    public class FakeFindCandidateStationService : IFindCandidateStationService
    {
        public Dictionary<ushort, float>? Result { get; set; }

        public Task<Dictionary<ushort, float>> GetCandidateStationFromCache(int evId) => Task.FromResult(Result!);

        public Action<IMiddlewareEvent> PreComputeCandidateStation() => throw new NotImplementedException();
    }

    public class FakeApplyNewPath : IApplyNewPath
    {
        public Position ApplyNewPathToEV(ref EV ev, Station station, Time currentTime) => new(0, 0);
    }
}
