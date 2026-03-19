namespace Engine.Metrics
{
    public class StationSnapshotMetric
    {
        public uint TotalQueueSize { get; set; }

        public Dictionary<uint, uint> ChargerQueueSizes { get; set; } = new();
    }
}
