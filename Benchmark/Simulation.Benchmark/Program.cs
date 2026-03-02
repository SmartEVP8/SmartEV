using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace MyBenchmarks
{
    public class OSRMRouterBenchmark
    {
        private OSRMRouter router;

        private const int TotalQueries = 1_000;

        [Params(1, 2, 4, 8, 16)] // different thread counts
        public int Parallelism { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            var path = "/home/mertz/Coding/SmartEV/Core/data/output.osrm";
            router = new OSRMRouter(path);
        }

        [Benchmark]
        public void QueryParallel()
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = Parallelism };

            try
            {
                Parallel.For(0, TotalQueries, options, i =>
                {
                    router.QuerySingleDestination(9.9410, 57.2706, 9.9217, 57.0488);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in benchmark: {ex}");
                throw;
            }
        }
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<OSRMRouterBenchmark>();
        }
    }
}
