namespace Headless.Benchmark;

using BenchmarkDotNet.Attributes;
using Engine.Routing;
using Core.Charging;

/// <summary>
/// Evaluates the performance of the OSRMRouter's QuerySingleDestination method under different levels of parallelism.
/// </summary>
public class OSRMRouterBenchmark
{
    private const int _totalQueries = 1_000;

    private OSRMRouter? _router;

    /// <summary>
    /// Gets or sets the degree of parallelism for the benchmark.
    /// </summary>
    [Params(1, 2, 4, 8, 16)] // different thread counts
    public int Parallelism { get; set; }

    /// <summary>
    /// OSRMRouter intialization before running the benchmark to ensure that the router is ready for querying.
    /// </summary>
    /// <exception cref="InvalidOperationException">OSRM data path not set.</exception>
    [GlobalSetup]
    public void Setup()
    {
        var path = AppContext.GetData("OsrmDataPath") as string
                        ?? throw new InvalidOperationException("OsrmDataPath not set in project.");
        _router = new OSRMRouter(new FileInfo(path), new List<Station>());
    }

    /// <summary>
    /// Cleans up resources after the benchmark is complete by disposing of the OSRMRouter instance.
    /// </summary>
    [Benchmark]
    public void QueryParallel()
    {
        var options = new ParallelOptions { MaxDegreeOfParallelism = Parallelism };

        try
        {
            Parallel.For(0, _totalQueries, options, i =>
            {
                _router?.QuerySingleDestination(9.9410, 57.2706, 9.9217, 57.0488);
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception in benchmark: {ex}");
            throw;
        }
    }
}
