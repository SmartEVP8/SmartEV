namespace Engine.Benchmark;

using BenchmarkDotNet.Diagnosers;
using Engine.Events;
using Engine.Vehicles;
using BenchmarkDotNet.Attributes;
using Core.Routing;
using Core.Shared;
using Core.Vehicles;
[MemoryDiagnoser]
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
public class UpdateAllEVsBenchMark
{
    private const int _count = 580000;
    private CheckAndUpdateAllEVsHandler _checkAndUpdateAllEVsHandler = null!;
    private EventScheduler _eventScheduler = null!;
    private EVStore _evStore = null!;

    /// <summary>
    /// Initializes the EventScheduler, EVStore, and CheckAndUpdateAllEVsHandler with a predefined number of EVs, each with a battery, preferences, and a journey.
    /// The EVs are initialized with a battery state of charge of 100% and a simple journey consisting of two waypoints.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _eventScheduler = new EventScheduler([]);
        _evStore = new EVStore(_count);
        var random = new Random(1);
        for (int i = 0; i < _count; i++)
        {
            var battery = new Battery(100, 50, 50, Socket.CCS2);
            var preferences = new Preferences(0, 0, 0);
            var journey = new Journey(0, 100, new Paths([new Position(10 * random.NextSingle(), 10 * random.NextSingle()), new Position(20 * random.NextSingle(), 20 * random.NextSingle())]));
            var ev = new EV(battery, preferences, journey, 10);
            _evStore.Set(i, ref ev);
        }

        _checkAndUpdateAllEVsHandler = new CheckAndUpdateAllEVsHandler(_eventScheduler, _evStore, 5, 10);
    }

    [IterationCleanup]
    public void IterationCleanup() => _eventScheduler = new EventScheduler([]);

    [Benchmark]
    public void UpdateAllEVs() => _checkAndUpdateAllEVsHandler.Handle(new CheckAndUpdateAllEVs(10));
}
