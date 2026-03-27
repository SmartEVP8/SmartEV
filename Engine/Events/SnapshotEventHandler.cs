namespace Engine.Events;

using Engine.Metrics;
using Engine.Services;
using Engine.Metrics.Snapshots;
using Core.Shared;
using Core.Charging;

/// <summary>
/// Handles the <see cref="SnapshotEvent"/> by collecting metrics for all stations
/// and rescheduling itself one hour later for the duration of the simulation.
/// </summary>
public class SnapshotEventHandler(
    Time rescheduleTime,
    DateTimeOffset startTime,
    IReadOnlyList<Station> stations,
    MetricsService metrics,
    EventScheduler scheduler,
    Func<ChargerBase, double> getDeliveredKW)
{
    private int _reservationCount = 0;

    private int _reservationCancelledCount = 0;

    /// <summary>
    /// Increments the reservation count, which will be recorded in the next snapshot. Called by the <see cref="StationService"/> whenever a reservation is made.
    /// </summary>
    public void OnReservationMade() => _reservationCount++;

    /// <summary>
    /// Increments the reservation cancelled count, which will be recorded in the next snapshot. Called by the <see cref="StationService"/> whenever a reservation is cancelled.
    /// </summary>
    public void OnReservationCancelled() => _reservationCancelledCount++;

    /// <summary>
    /// Gets the current count of reservations made since the last snapshot. Used for testing to verify that counts are reset after each snapshot.
    /// </summary>
    internal int ReservationCount => _reservationCount;

    /// <summary>
    /// Gets the current count of reservations cancelled since the last snapshot. Used for testing to verify that counts are reset after each snapshot.
    /// </summary>
    internal int ReservationCancelledCount => _reservationCancelledCount;

    /// <summary>
    /// Iterates over all stations, collects a <see cref="StationSnapshotMetric"/> for each,
    /// records them via the <see cref="MetricsService"/>, then reschedules the next
    /// snapshot one hour later.
    /// </summary>
    /// <param name="e">The snapshot event containing the station list, metrics service,
    /// scheduler, and power delivery.</param>
    public void Handle(SnapshotEvent e)
    {
        var currentTime = startTime.AddSeconds(e.Time.T);

        // Station snapshots
        foreach (var station in stations)
        {
            var metric = StationSnapshotMetric.Collect(
                station,
                e.Time,
                currentTime.DayOfWeek,
                currentTime.Hour,
                getDeliveredKW);
            metrics.RecordSnapshot(metric);
        }

        // Reservation snapshot
        metrics.RecordReservationSnapshot(new ReservationSnapshotMetric
        {
            SimTime = e.Time,
            TotalReservations = _reservationCount,
        });
        _reservationCount = 0;

        // Reservation cancellation snapshot
        metrics.RecordReservationCancellationSnapshot(new ReservationCancellationSnapshotMetric
        {
            SimTime = e.Time,
            TotalReservationCancellations = _reservationCancelledCount,
        });
        _reservationCancelledCount = 0;

        scheduler.ScheduleEvent(new SnapshotEvent(e.Time + rescheduleTime));
    }
}