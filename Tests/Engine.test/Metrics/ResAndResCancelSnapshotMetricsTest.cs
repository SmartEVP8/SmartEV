namespace Engine.test.Metrics;

using Core.Shared;
using Engine.Events;
using Engine.Metrics;

public class ReservationAndReservationCancellationSnapshotMetricsTests
{
    [Fact]
    public void ReservationAndCancellationCounts_RecordedInSnapshot_ThenReset()
    {
        var handler = Handler();

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

    private static SnapshotEventHandler Handler() =>
        new(
            rescheduleTime: new Time(3600),
            startTime: DateTimeOffset.UtcNow,
            stations: [],
            metrics: new MetricsService(new MetricsConfig(), Guid.NewGuid()),
            scheduler: new EventScheduler([]),
            getDeliveredKW: _ => 0.0);
}