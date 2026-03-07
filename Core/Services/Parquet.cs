namespace Core.Services;

using Parquet;
using Parquet.Data;
using Parquet.Schema;

/// <summary>
/// Writes data to a Parquet file
/// Create once at the start, append afterwards, dispose at the end.
/// </summary>
public sealed class Writer : IAsyncDisposable
{
    private readonly FileStream _stream;
    private readonly ParquetWriter _writer;

    private Writer(FileStream stream, ParquetWriter writer)
    {
        _stream = stream;
        _writer = writer;
    }

    /// <summary>
    /// Creates a new Parquet file at <paramref name="outputPath"/> and initializes the writer.
    /// Overwrites any existing file at that path.
    /// </summary>
    /// <param name="outputPath">The path to the output Parquet file.</param>
    /// <param name="schema">The schema defining the columns of the Parquet file.</param>
    /// <returns>A fully initialized <see cref="Writer"/> ready to accept appends.</returns>
    public static async Task<Writer> CreateAsync(string outputPath, ParquetSchema schema)
    {
        var stream = File.Open(outputPath, FileMode.Create);
        var writer = await ParquetWriter.CreateAsync(schema, stream);
        return new Writer(stream, writer);
    }

    /// <summary>
    /// Appends a new row group to the Parquet file with the provided columns.
    /// Column order must match the schema defined in <see cref="CreateAsync"/>.
    /// </summary>
    /// <param name="columns">The data columns to write, one per schema field.</param>
    /// <returns>A task that completes when the row group has been written.</returns>
    public async Task AppendAsync(params DataColumn[] columns)
    {
        using var rowGroup = _writer.CreateRowGroup();
        foreach (var column in columns)
            await rowGroup.WriteColumnAsync(column);
    }

    /// <summary>
    /// Finalizes and closes the Parquet file, flushing the footer.
    /// </summary>
    /// <returns>A task that completes when the file has been finalized and closed.</returns>
    public async ValueTask DisposeAsync()
    {
        _writer.Dispose();
        await _stream.DisposeAsync();
    }
}