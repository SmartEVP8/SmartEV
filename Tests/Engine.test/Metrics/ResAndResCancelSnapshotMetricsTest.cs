namespace Engine.test.Metrics;

using Core.Shared;
using Engine.Events;
using Engine.test.Builders;

public class ReservationAndReservationCancellationSnapshotMetricsTests
{
    [Fact]
    public void ReservationAndCancellationCounts_RecordedInSnapshot_ThenReset()
    {
        var handler = TestData.SnapshotHandler(
            TestData.MetricsService(),
            new EventScheduler([]),
            []);

        handler.OnReservationMade();
        handler.OnReservationMade();
        handler.OnReservationMade();
        handler.OnReservationCancelled();
        handler.OnReservationCancelled();

        Assert.Equal(3, handler.ReservationCount);
        Assert.Equal(2, handler.ReservationCancelledCount);

        handler.Handle(new SnapshotEvent(new Time(0)));

        Assert.Equal(0, handler.ReservationCount);
        Assert.Equal(0, handler.ReservationCancelledCount);

        handler.OnReservationMade();
        Assert.Equal(1, handler.ReservationCount);
    }
}