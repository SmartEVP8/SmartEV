namespace Core.Services;

using ParquetSharp;

/// <summary>
/// Provides generic read and write operations for Parquet files.
/// </summary>
public static class ParquetService
{
    /// <summary>
    /// Reads columnar data from a Parquet file at the specified path.
    /// Only the first row group is read.
    /// </summary>
    /// <param name="path">The file path of the Parquet file to read.</param>
    /// <returns>A dictionary mapping column names to typed arrays of data.</returns>
    /// <exception cref="NotSupportedException">Thrown when a column contains an unsupported element type.</exception>
    public static IReadOnlyDictionary<string, Array> Read(string path)
    {
        using var file = new ParquetFileReader(path);

        var numColumns = file.FileMetaData.NumColumns;
        var numRows = (int)file.FileMetaData.NumRows;
        var schema = file.FileMetaData.Schema;
        var result = new Dictionary<string, Array>();

        using var rowGroup = file.RowGroup(0);
        for (var col = 0; col < numColumns; col++)
        {
            var columnName = schema.Column(col).Name;
            var logicalReader = rowGroup.Column(col).LogicalReader();

            result[columnName] = logicalReader switch
            {
                LogicalColumnReader<float> r => r.ReadAll(numRows),
                LogicalColumnReader<double> r => r.ReadAll(numRows),
                LogicalColumnReader<int> r => r.ReadAll(numRows),
                LogicalColumnReader<ushort> r => r.ReadAll(numRows),
                LogicalColumnReader<uint> r => r.ReadAll(numRows),
                LogicalColumnReader<ulong> r => r.ReadAll(numRows),
                LogicalColumnReader<short> r => r.ReadAll(numRows),
                LogicalColumnReader<long> r => r.ReadAll(numRows),
                LogicalColumnReader<string> r => r.ReadAll(numRows),
                LogicalColumnReader<bool> r => r.ReadAll(numRows),
                _ => throw new NotSupportedException($"Unsupported column type for '{columnName}'")
            };
        }

        return result;
    }

    /// <summary>
    /// Writes columnar data to a Parquet file at the specified path.
    /// </summary>
    /// <param name="path">The file path where the Parquet file will be written.</param>
    /// <param name="columns">A dictionary mapping column names to typed arrays of data.</param>
    /// <exception cref="InvalidOperationException">Thrown when a column value is not a typed array.</exception>
    /// <exception cref="NotSupportedException">Thrown when a column contains an unsupported element type.</exception>
    public static void Write(string path, IReadOnlyDictionary<string, Array> columns)
    {
        var schema = columns.Keys.Select(name =>
        {
            var elementType = columns[name].GetType().GetElementType()
                ?? throw new InvalidOperationException($"Column '{name}' does not have an array type");
            return CreateColumn(name, elementType);
        }).ToArray();

        using var file = new ParquetFileWriter(path, schema);
        using var rowGroup = file.AppendRowGroup();

        foreach (var (_, data) in columns)
            WriteColumn(rowGroup, data);

        file.Close();
    }

    /// <summary>
    /// Writes a single typed array to the next column in the row group.
    /// </summary>
    /// <param name="rowGroup">The row group writer to write into.</param>
    /// <param name="data">The typed array of data to write.</param>
    /// <exception cref="NotSupportedException">Thrown when the array element type is not supported.</exception>
    private static void WriteColumn(RowGroupWriter rowGroup, Array data)
    {
        switch (data)
        {
            case float[] f:
                using (var w = rowGroup.NextColumn().LogicalWriter<float>()) w.WriteBatch(f); break;
            case double[] d:
                using (var w = rowGroup.NextColumn().LogicalWriter<double>()) w.WriteBatch(d); break;
            case int[] i:
                using (var w = rowGroup.NextColumn().LogicalWriter<int>()) w.WriteBatch(i); break;
            case long[] l:
                using (var w = rowGroup.NextColumn().LogicalWriter<long>()) w.WriteBatch(l); break;
            case ushort[] us:
                using (var w = rowGroup.NextColumn().LogicalWriter<ushort>()) w.WriteBatch(us); break;
            case uint[] ui:
                using (var w = rowGroup.NextColumn().LogicalWriter<uint>()) w.WriteBatch(ui); break;
            case ulong[] ul:
                using (var w = rowGroup.NextColumn().LogicalWriter<ulong>()) w.WriteBatch(ul); break;
            case short[] sh:
                using (var w = rowGroup.NextColumn().LogicalWriter<short>()) w.WriteBatch(sh); break;
            case string[] s:
                using (var w = rowGroup.NextColumn().LogicalWriter<string>()) w.WriteBatch(s); break;
            case bool[] b:
                using (var w = rowGroup.NextColumn().LogicalWriter<bool>()) w.WriteBatch(b); break;
            default:
                throw new NotSupportedException($"Unsupported column type: {data.GetType().GetElementType()}");
        }
    }

    /// <summary>
    /// Creates a typed Parquet column definition for the given name and element type.
    /// </summary>
    /// <param name="name">The column name.</param>
    /// <param name="type">The element type of the column data.</param>
    /// <returns>A typed <see cref="Column"/> definition.</returns>
    /// <exception cref="NotSupportedException">Thrown when the type is not supported.</exception>
    private static Column CreateColumn(string name, Type type) => type switch
    {
        _ when type == typeof(float) => new Column<float>(name),
        _ when type == typeof(double) => new Column<double>(name),
        _ when type == typeof(int) => new Column<int>(name),
        _ when type == typeof(ushort) => new Column<ushort>(name),
        _ when type == typeof(uint) => new Column<uint>(name),
        _ when type == typeof(ulong) => new Column<ulong>(name),
        _ when type == typeof(short) => new Column<short>(name),
        _ when type == typeof(long) => new Column<long>(name),
        _ when type == typeof(string) => new Column<string>(name),
        _ when type == typeof(bool) => new Column<bool>(name),
        _ => throw new NotSupportedException($"Unsupported column type: {type}")
    };
}