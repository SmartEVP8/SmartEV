namespace Engine.Metrics;

/// <summary>
/// Controls the directory structure and file paths for metric output.
/// </summary>
public class MetricsFileManager
{
    private readonly DirectoryInfo _metricsDirectory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsFileManager"/> class.
    /// Creates a new MetricsFileManager for the given output directory and simulation run ID.
    /// </summary>
    /// <param name="di">Directory for where all the metrics are.</param>
    /// <param name="id">Name of the folder where metrics for a run is stored.</param>
    public MetricsFileManager(DirectoryInfo di, Guid id)
    {
        di.Create();
        _metricsDirectory = di.CreateSubdirectory(id.ToString());
    }

    /// <summary>
    /// Returns the path to the parquet file for the given metric type T.
    /// </summary>
    /// <typeparam name="T">Used to set the filename.</typeparam>
    /// <returns>Fileinfo for the location to save metric data.</returns>
    public FileInfo GetMetricPath<T>() => new(Path.Combine(_metricsDirectory.FullName, $"{typeof(T).Name}.parquet"));
}
