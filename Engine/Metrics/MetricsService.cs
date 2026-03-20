namespace Engine.Metrics;

using Engine.Metrics.Snapshots;

/// <summary>
/// <para>Entry point for all metric recording during a simulation run.</para>
/// <para>
/// Each enabled metric type gets its own MetricWriter — its own channel,
/// buffer, writer task, and parquet file. They operate fully independently
/// and drain in parallel at the end of the simulation.
/// </para>
/// <para>
/// Usage:
///   1. Construct with a MetricsConfig
///   2. Call Record* methods from the sim thread freely — they never block
///   3. Call StopAsync() once at simulation end to drain and flush everything.
/// </para>
/// </summary>
public sealed class MetricsService : IAsyncDisposable
{
    private readonly IMetricWriter<EVSnapshotMetric>? _cars;
    private readonly IMetricWriter<StationSnapshotMetric>? _stations;
    private readonly IMetricWriter<DeadlineMetric>? _deadlines;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsService"/> class.
    /// Creates writers for each enabled metric type based on the provided config.
    /// </summary>
    /// <param name="config">The configuration specifying which metric types to record and where to store them.</
    /// <param name="runId">The unique identifier for the simulation run, used to organize output files.</param>
    public MetricsService(MetricsConfig config, Guid runId)
    {
        var files = new MetricsFileManager(config.OutputDirectory, runId);

        if (config.RecordCarSnapshots)
            _cars = new MetricWriter<EVSnapshotMetric>(config.BufferSize, files.GetMetricPath<EVSnapshotMetric>());
        if (config.RecordStationSnapshots)
            _stations = new MetricWriter<StationSnapshotMetric>(config.BufferSize, files.GetMetricPath<StationSnapshotMetric>());
        if (config.RecordDeadlines)
            _deadlines = new MetricWriter<DeadlineMetric>(config.BufferSize, files.GetMetricPath<DeadlineMetric>());
    }

    /// <summary>Records a car snapshot. No-op if car snapshots are disabled in config.</summary>
    /// <param name="metric">The car snapshot metric to record.</param>
    public void RecordCar(EVSnapshotMetric metric) => _cars?.Record(metric);

    /// <summary>Records a station snapshot. No-op if station snapshots are disabled in config.</summary>
    /// <param name="metric">The station snapshot metric to record.</param>
    public void RecordStation(StationSnapshotMetric metric) => _stations?.Record(metric);

    /// <summary>Records a deadline metric. No-op if deadlines are disabled in config.</summary>
    /// <param name="metric">The deadline metric to record.</param>
    public void RecordDeadline(DeadlineMetric metric) => _deadlines?.Record(metric);

    /// <summary>
    /// Signals all writers to stop, drains their channels, and flushes remaining
    /// buffered metrics to parquet. All writers drain in parallel.
    /// </summary>
    /// <returns>A task that completes once all metrics have been flushed and all writers have fully stopped. </returns>
    public async ValueTask DisposeAsync()
    {
        var tasks = new List<Task>();
        if (_cars is not null) tasks.Add(_cars.DisposeAsync().AsTask());
        if (_stations is not null) tasks.Add(_stations.DisposeAsync().AsTask());
        if (_deadlines is not null) tasks.Add(_deadlines.DisposeAsync().AsTask());
        await Task.WhenAll(tasks);
    }
}
