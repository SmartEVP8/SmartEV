namespace Engine.Events;

using Engine.Metrics;
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
    /// <summary>
    /// Iterates over all stations, collects a <see cref="SnapshotMetric"/> for each,
    /// records them via the <see cref="MetricsService"/>, then reschedules the next
    /// snapshot one hour later.
    /// </summary>
    /// <param name="e">The snapshot event containing the station list, metrics service,
    /// scheduler, and power delivery.</param>
    /// <param name="currentTime">The current simulation time in seconds.</param>
    public void Handle(SnapshotEvent e)
    {
        var currentTime = startTime.AddSeconds(e.Time.T);
        var day = currentTime.DayOfWeek;
        var hour = currentTime.Hour;

        foreach (var station in stations)
        {
            var metric = SnapshotMetric.Collect(
                station, (uint)e.Time,
                currentTime.DayOfWeek, 
                currentTime.Hour,
                getDeliveredKW
                );
            metrics.RecordSnapshot(metric);
        }

        var next = new SnapshotEvent(e.Time + rescheduleTime);
        scheduler.ScheduleEvent(next);
    }
}