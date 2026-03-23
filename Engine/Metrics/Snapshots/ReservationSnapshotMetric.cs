namespace Engine.Metrics.Snapshots;

using Core.Shared;
public class ReservationMetric
{
    public int TotalRequests { get; set; }
    public Dictionary<int, Time> RequestTimestamps { get; } = [];
    public Dictionary<int, float> PathDeviations { get; } = [];
}