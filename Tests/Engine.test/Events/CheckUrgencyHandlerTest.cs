namespace Testing;

using Engine.Events;
using Core.Vehicles;
using Core.Routing;
using Engine.Vehicles;
using Core.Shared;

public class CheckUrgencyHandlerTest
{
    private EventScheduler _scheduler;
    private EVStore _evStore;

    public CheckUrgencyHandlerTest()
    {
        _scheduler = new EventScheduler();
        _evStore = new EVStore(10);
    }

    [Fact]
    public void LowUrgencySchedulesFindCandidate()
    {
        var stateOfCharge = 50f;
        var ev = new EV(
            battery: new Battery(capacity: 50, maxChargeRate: 20, stateOfCharge: stateOfCharge, socket: Socket.CCS2),
            efficiency: 2,
            preferences: new Preferences(priceSensitivity: 0.5f, minAcceptableCharge: 0.1f),
            journey: new Journey(0, 100, new Paths([new Position(10, 10), new Position(20, 20)])));
        _evStore.Set(1, ref ev);

        var urgencyEvent = new CheckUrgency(1, 0);
        var handler = new CheckUrgencyHandler(_scheduler, _evStore, 5, new Random(42));
        handler.Handle(urgencyEvent);
        var nextEvent = _scheduler.GetNextEvent();
        Assert.IsType<CheckUrgency>(nextEvent);
        var noNextEvent = _scheduler.GetNextEvent();
        Assert.Null(noNextEvent);
    }

    [Fact]
    public void HighUrgencySchedulesFindCandidate()
    {
        var stateOfCharge = 4f;
        var ev = new EV(
            battery: new Battery(capacity: 50, maxChargeRate: 20, stateOfCharge: stateOfCharge, socket: Socket.CCS2),
            efficiency: 2,
            preferences: new Preferences(priceSensitivity: 0.5f, minAcceptableCharge: 0.1f),
            journey: new Journey(0, 100, new Paths([new Position(10, 10), new Position(20, 20)])));
        _evStore.Set(1, ref ev);

        var urgencyEvent = new CheckUrgency(1, 0);
        var handler = new CheckUrgencyHandler(_scheduler, _evStore, 5, new Random(42));
        handler.Handle(urgencyEvent);
        var nextEvent = _scheduler.GetNextEvent();
        Assert.IsType<FindCandidate>(nextEvent);
        var nextCheckUrgencyEvent = _scheduler.GetNextEvent();
        Assert.IsType<CheckUrgency>(nextCheckUrgencyEvent);
        var noNextEvent = _scheduler.GetNextEvent();
        Assert.Null(noNextEvent);
    }
}
