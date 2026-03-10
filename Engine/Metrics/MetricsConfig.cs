namespace Engine.Metrics;

/// <summary>
/// Controls which metric types are recorded during a simulation run.
/// Unregistered types cost nothing — their writers are never created.
/// </summary>
public sealed class MetricsConfig
{
    public int BufferSize { get; init; } = 1024;
    public DirectoryInfo OutputDirectory { get; init; } = new DirectoryInfo("metrics");
    public bool RecordCarSnapshots { get; init; }
    public bool RecordStationSnapshots { get; init; }
    public bool RecordDeadlines { get; init; }
}

