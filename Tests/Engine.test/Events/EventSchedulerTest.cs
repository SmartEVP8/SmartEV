namespace Testing;

using Engine.Events;

public class EventSchedulerTest
{
    private EventScheduler _scheduler;

    public EventSchedulerTest()
    {
        _scheduler = new EventScheduler();
    }

    [Fact]
    public void ScheduleEventTest()
    {
        var request1 = new ReservationRequest(1, 1, 10);
        var request2 = new ReservationRequest(2, 1, 20);
        var request3 = new ReservationRequest(3, 1, 15);

        _scheduler.ScheduleEvent(request1);
        _scheduler.ScheduleEvent(request2);
        _scheduler.ScheduleEvent(request3);

        Assert.Equal(request1, _scheduler.GetNextEvent());
        Assert.Equal(request3, _scheduler.GetNextEvent());
        Assert.Equal(request2, _scheduler.GetNextEvent());
    }

    [Fact]
    public void CancelEventTest()
    {
        var request1 = new ReservationRequest(1, 1, 10);
        var request2 = new ReservationRequest(2, 1, 20);
        var request3 = new ReservationRequest(3, 1, 15);

        _scheduler.ScheduleEvent(request1);
        _scheduler.ScheduleEvent(request2);
        _scheduler.ScheduleEvent(request3);

        _scheduler.CancelEvent(new CancelRequest(request2.EVId, request2.StationId, 123));

        Assert.Equal(request1, _scheduler.GetNextEvent());
        Assert.Equal(request3, _scheduler.GetNextEvent());
        Assert.Null(_scheduler.GetNextEvent());
    }

    [Fact]
    public void CancelTooManyEventsTest()
    {
        var request1 = new ReservationRequest(1, 1, 10);
        var request2 = new ReservationRequest(2, 1, 20);
        var request3 = new ReservationRequest(3, 1, 15);
        var request4 = new ReservationRequest(2, 1, 25);

        _scheduler.ScheduleEvent(request1);
        _scheduler.ScheduleEvent(request2);
        _scheduler.ScheduleEvent(request3);
        _scheduler.ScheduleEvent(request4);

        _scheduler.CancelEvent(new CancelRequest(request2.EVId, request2.StationId, 123));

        Assert.Equal(request1, _scheduler.GetNextEvent());
        Assert.Equal(request3, _scheduler.GetNextEvent());
        Assert.Equal(request4, _scheduler.GetNextEvent());
        Assert.Null(_scheduler.GetNextEvent());
    }
}
