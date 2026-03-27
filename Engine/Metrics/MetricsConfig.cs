namespace Engine.Metrics;

/// <summary>
/// Controls which metric types are recorded during a simulation run.
/// </summary>
public sealed class MetricsConfig
{
    /// <summary>Gets the buffer size for metric writers.</summary>
    public int BufferSize { get; init; } = 1024;

    /// <summary>Gets the output directory for metric files.</summary>
    public DirectoryInfo OutputDirectory { get; init; } = new DirectoryInfo("metrics");

    /// <summary>Gets a value indicating whether car snapshots are recorded.</summary>
    public bool RecordCarSnapshots { get; init; }

    /// <summary>Gets a value indicating whether station snapshots are recorded.</summary>
    public bool RecordStationSnapshots { get; init; }

    /// <summary> Gets a value indicating whether reservation events are recorded. </summary>
    public bool RecordReservations { get; init; }

    /// <summary> Gets a value indicating whether reservation cancellation events are recorded. </summary>
    public bool RecordReservationCancellations { get; init; }

    /// <summary>Gets a value indicating whether arrivals are recorded.</summary>
    public bool RecordArrivals { get; init; }

    /// <summary>Gets a value indicating whether single station snapshots are recorded.</summary>
    public bool RecordSingleStationSnapshot { get; init; }
}