namespace Engine.Metrics;

using Engine.Metrics.Events;
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
    private readonly IMetricWriter<ArrivalAtDestinationMetric>? _arrivals;
    private readonly IMetricWriter<StationSnapshotMetric>? _stations;
    private readonly IMetricWriter<ChargerSnapshotMetric>? _chargers;
    private readonly IMetricWriter<WaitTimeInQueueMetric>? _waitTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsService"/> class.
    /// Creates writers for each enabled metric type based on the provided config.
    /// </summary>
    /// <param name="config">The configuration specifying which metric types to record and where to store them.</param>
    /// <param name="runId">The unique identifier for the simulation run, used to organize output files.</param>
    public MetricsService(MetricsConfig config, Guid runId)
    {
        var files = new MetricsFileManager(config.OutputDirectory, runId);

        if (config.RecordCarSnapshots)
            _cars = new MetricWriter<EVSnapshotMetric>(config.BufferSize, files.GetMetricPath<EVSnapshotMetric>());
        if (config.RecordArrivals)
            _arrivals = new MetricWriter<ArrivalAtDestinationMetric>(config.BufferSize, files.GetMetricPath<ArrivalAtDestinationMetric>());
        if (config.RecordStationSnapshots)
            _stations = new MetricWriter<StationSnapshotMetric>(config.BufferSize, files.GetMetricPath<StationSnapshotMetric>());
        if (config.RecordChargerSnapshots)
            _chargers = new MetricWriter<ChargerSnapshotMetric>(config.BufferSize, files.GetMetricPath<ChargerSnapshotMetric>());
        if (config.RecordEVWaitTime)
            _waitTime = new MetricWriter<WaitTimeInQueueMetric>(config.BufferSize, files.GetMetricPath<WaitTimeInQueueMetric>());
    }

    /// <summary>Records a car snapshot. No-op if car snapshots are disabled in config.</summary>
    /// <param name="metric">The car snapshot metric to record.</param>
    public void RecordCar(EVSnapshotMetric metric) => _cars?.Record(metric);

    /// <summary>Records an arrival metric. No-op if arrivals are disabled in config.</summary>
    /// <param name="metric">The arrival metric to record.</param>
    public void RecordArrival(ArrivalAtDestinationMetric metric) => _arrivals?.Record(metric);

    /// <summary>Records a station snapshot metric. No-op if station snapshots are disabled in config.</summary>
    /// <param name="metric">The station snapshot metric to record.</param>
    public void RecordStationSnapshot(StationSnapshotMetric metric) => _stations?.Record(metric);

    /// <summary>Records a charger snapshot metric. No-op if charger snapshots are disabled in config.</summary>
    /// <param name="metric">The charger snapshot metric to record.</param>
    public void RecordChargerSnapshot(ChargerSnapshotMetric metric) => _chargers?.Record(metric);

    /// <summary> Records an EV wait time metric. No-op if EV wait time metrics are disabled in config. </summary>
    /// <param name="metric">The EV wait time metric to record.</param>
    public void RecordWaitTime(WaitTimeInQueueMetric metric) => _waitTime?.Record(metric);

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
        if (_chargers is not null) tasks.Add(_chargers.DisposeAsync().AsTask());
        if (_arrivals is not null) tasks.Add(_arrivals.DisposeAsync().AsTask());
        if (_waitTime is not null) tasks.Add(_waitTime.DisposeAsync().AsTask());
        await Task.WhenAll(tasks);
    }
}
