namespace Engine.Events;

using Engine.Metrics;

/// <summary>
/// Handles the <see cref="SnapshotEvent"/> by collecting metrics for all stations
/// and rescheduling itself one hour later for the duration of the simulation.
/// </summary>
public class SnapshotEventHandler
{
    private const uint _timeBeforeReschedule = 3600;

    /// <summary>
    /// Iterates over all stations, collects a <see cref="SnapshotMetric"/> for each,
    /// records them via the <see cref="MetricsService"/>, then reschedules the next
    /// snapshot one hour later.
    /// </summary>
    /// <param name="e">The snapshot event containing the station list, metrics service,
    /// scheduler, and power delivery.</param>
    /// <param name="currentTime">The current simulation time in seconds.</param>
    public void Handle(SnapshotEvent e, uint currentTime)
    {
        var wallTime = DateTimeOffset.FromUnixTimeSeconds(currentTime);

        foreach (var station in e.Stations)
        {
            var metric = SnapshotMetric.Collect(
                station, currentTime,
                wallTime.DayOfWeek, wallTime.Hour,
                e.GetDeliveredKW);
            e.Metrics.RecordSnapshot(metric);
        }

        var next = new SnapshotEvent(e.Stations, e.Metrics, e.Scheduler, e.GetDeliveredKW);
        e.Scheduler.ScheduleEvent(next, currentTime + _timeBeforeReschedule);
    }
}