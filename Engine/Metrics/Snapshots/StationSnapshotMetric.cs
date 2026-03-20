namespace Engine.Metrics.Snapshots;

public class StationSnapshotMetric
{
    public int TotalQueueSize { get; set; }

    public Dictionary<uint, int> ArrivalTimes { get; } = [];
}
