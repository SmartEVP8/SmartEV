namespace Testing;

using Core.test.Builders;
using Engine.Events;

public class EventSchedulerTest
{
    private EventScheduler _scheduler;
    public EventSchedulerTest() => _scheduler = new EventScheduler();

    [Fact]
    public void CancelEventTest()
    {
        var ev = CoreTestData.EV();
        var charger = CoreTestData.SingleCharger(1);
        var station = CoreTestData.Station(1);

        var request1 = new EndCharging(ev, charger, station, 10);
        var request2 = new EndCharging(ev, charger, station, 20);
        var request3 = new EndCharging(ev, charger, station, 15);

        _scheduler.ScheduleEvent(request1);
        var ct = _scheduler.ScheduleEvent(request2);
        _scheduler.ScheduleEvent(request3);
        _scheduler.CancelEvent(ct);

        Assert.Equal(request1, _scheduler.GetNextEvent());
        Assert.Equal(request3, _scheduler.GetNextEvent());
        Assert.Null(_scheduler.GetNextEvent());
    }

    [Fact]
    public void CancelWithManyDifferentEventsTest()
    {
        var ev = CoreTestData.EV();
        var charger = CoreTestData.SingleCharger(1);
        var station = CoreTestData.Station(1);

        var request1 = new ArriveAtStation(ev, station, 0.6, 20);
        var request2 = new EndCharging(ev, charger, station, 15);
        var request3 = new ArriveAtDestination(ev, 25);

        _scheduler.ScheduleEvent(request1);
        var ct = _scheduler.ScheduleEvent(request2);
        _scheduler.ScheduleEvent(request3);
        _scheduler.CancelEvent(ct);

        Assert.Equal(request1, _scheduler.GetNextEvent());
        Assert.Equal(request3, _scheduler.GetNextEvent());
        Assert.Null(_scheduler.GetNextEvent());
    }
}
