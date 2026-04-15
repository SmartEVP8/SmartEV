namespace Engine.Events;

using Engine.Metrics;
using Engine.Metrics.Snapshots;
using Engine.Services;
using Core.Shared;

/// <summary>
/// Handles the <see cref="SnapshotEvent"/> by collecting metrics for all stations and chargers via the <see cref="StationService"/>,
/// and rescheduling itself later.
/// </summary>
public class SnapshotEventHandler(
    Time rescheduleTime,
    MetricsService metrics,
    StationMetricsCollector stationMetricsCollector,
    EventScheduler scheduler)
{
    /// <summary>
    /// Iterates over all stations, collects a <see cref="StationSnapshotMetric"/> for each,
    /// records them via the <see cref="MetricsService"/>, then reschedules the next
    /// snapshot.
    /// Also collects an <see cref="EVSnapshotMetric"/>.
    /// </summary>
    /// <param name="e">The snapshot event containing the station list, metrics service,
    /// scheduler, and power delivery.</param>
    public void Handle(SnapshotEvent e)
    {
        var (chargerMetrics, stationMetrics) = stationMetricsCollector.Collect(rescheduleTime, e.Time);

        foreach (var stationMetric in stationMetrics)
            metrics.RecordStationSnapshot(stationMetric);

        foreach (var chargerMetric in chargerMetrics)
            metrics.RecordChargerSnapshot(chargerMetric);

        scheduler.ScheduleEvent(new SnapshotEvent(e.Time + rescheduleTime));
    }
}
