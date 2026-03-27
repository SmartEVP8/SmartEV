namespace Engine.Events;

using Engine.Metrics;
using Engine.Services;
using Engine.Metrics.Snapshots;
using Core.Shared;
using Core.Charging;
using Engine.Vehicles;

/// <summary>
/// Handles the <see cref="SnapshotEvent"/> by collecting metrics for all stations
/// and rescheduling itself one hour later for the duration of the simulation.
/// </summary>
public class SnapshotEventHandler(
    Time rescheduleTime,
    DateTimeOffset startTime,
    Dictionary<ushort, Station> stations,
    MetricsService metrics,
    EventScheduler scheduler)
{
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

        foreach (var station in stations.Values)
        {
            var metric = StationSnapshotMetric.Collect(
                station,
                e.Time,
                currentTime.DayOfWeek,
                currentTime.Hour);
            metrics.RecordStationSnapshot(metric);
        }

        scheduler.ScheduleEvent(new SnapshotEvent(e.Time + rescheduleTime));
    }
}