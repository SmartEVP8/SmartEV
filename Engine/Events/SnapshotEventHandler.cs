namespace Engine.Events;

using Engine.Metrics;

public static class SnapshotEventHandler
{
    private const uint OneHour = 3600;

    public static void Handle(SnapshotEvent e, uint currentTime)
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
        e.Scheduler.ScheduleEvent(next, currentTime + OneHour);
    }
}