namespace Engine.Metrics;

using System.Threading.Channels;
using Parquet;
using Parquet.Serialization;

/// <summary>
/// <para>Owns a single-reader channel, an in-memory buffer, and a parquet output file for metric type T.</para>
/// <para>
/// Threading model:
///   Sim thread  → Record() → TryWrite into channel (non-blocking, struct copy)
///   Writer task → reads channel, accumulates buffer, flushes to parquet when full.
/// </para>
/// <para>The sim thread never waits on I/O. Parquet writes happen entirely on the writer task.</para>
/// </summary>
/// <typeparam name="T">Metric type.</typeparam>
///
public sealed class MetricWriter<T> : IMetricWriter<T>
{
    private readonly Channel<T> _channel;
    private readonly Task _writerTask;
    private readonly int _bufferSize;
    private readonly FileInfo _path;
    private bool _hasWritten;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricWriter{T}"/> class.
    /// Initializes the writer with a channel, buffer size, and parquet file path.
    /// </summary>
    /// <param name="bufferSize">The amount of entries before flushing and creating a new rowgroup.</param>
    /// <param name="path">The path where the parquet file will be stored.</param>
    public MetricWriter(int bufferSize, FileInfo path)
    {
        _bufferSize = bufferSize;
        _path = path;
        _channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
        _writerTask = Task.Run(DrainAsync);
        _hasWritten = false;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        await _writerTask;
    }

    /// <inheritdoc/>
    public void Record(T metric) => _channel.Writer.TryWrite(metric);

    /// <summary>
    /// Runs on the writer task for the lifetime of the simulation.
    /// Accumulates metrics into a buffer and flushes to parquet when the buffer is full.
    /// On channel completion, flushes whatever remains.
    /// </summary>
    private async Task DrainAsync()
    {
        var buffer = new List<T>(_bufferSize);

        await foreach (var metric in _channel.Reader.ReadAllAsync())
        {
            buffer.Add(metric);
            if (buffer.Count == _bufferSize)
                await FlushAsync(buffer);
        }

        if (buffer.Count > 0)
            await FlushAsync(buffer);
    }

    /// <summary>
    /// Serializes the buffer as a parquet row group and clears it.
    /// Appends to the file so each flush becomes a new row group.
    /// </summary>
    private async Task FlushAsync(List<T> buffer)
    {
        var options = new ParquetSerializerOptions
        {
            CompressionMethod = CompressionMethod.Snappy,
            RowGroupSize = buffer.Count,
            Append = _hasWritten,
        };
        await ParquetSerializer.SerializeAsync(buffer, _path.ToString(), options);
        buffer.Clear();
        _hasWritten = true;
    }
}
