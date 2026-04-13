namespace Testing;

using Engine.Events;

public class EventSchedulerTest
{
    private EventScheduler _scheduler;

    public EventSchedulerTest() => _scheduler = new EventScheduler();

    [Fact]
    public void CancelEventTest()
    {
        var request1 = new EndCharging(1, 1, 0, 10);
        var request2 = new EndCharging(2, 1, 0, 20);
        var request3 = new EndCharging(3, 1, 0, 15);

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
        var request1 = new ArriveAtStation(2, 1, 0.6f, 20);
        var request2 = new EndCharging(2, 1, 0, 15);
        var request3 = new ArriveAtDestination(2, 25);

        _scheduler.ScheduleEvent(request1);
        var ct = _scheduler.ScheduleEvent(request2);
        _scheduler.ScheduleEvent(request3);

        _scheduler.CancelEvent(ct);

        Assert.Equal(request1, _scheduler.GetNextEvent());
        Assert.Equal(request3, _scheduler.GetNextEvent());
        Assert.Null(_scheduler.GetNextEvent());
    }
}
