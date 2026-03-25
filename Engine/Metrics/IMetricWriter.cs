namespace Engine.Metrics;

/// <summary>
/// Defines a writer for a specific metric type.
/// Implementations own the channel, buffer, and parquet file for that type.
/// </summary>
/// <typeparam name="T">Metric type.</typeparam>
public interface IMetricWriter<T> : IAsyncDisposable
{
    /// <summary>
    /// Records a metric by writing it into the channel. This method is non-blocking and should never wait on I/O.
    /// </summary>
    /// <param name="metric">The metric type.</param>
    void Record(T metric);
}
