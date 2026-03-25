using BenchmarkDotNet.Attributes;
using Core.Routing;
using Core.Shared;
using Core.Vehicles;
namespace Engine.Benchmark;

using Engine.Events;
using Engine.Vehicles;

[MemoryDiagnoser]
[MaxIterationCount(35)]
public class UpdateAllEVsBenchMark
{
    private const int _count = 580000;
    private CheckAndUpdateAllEVsHandler _checkAndUpdateAllEVsHandler = null!;
    private EventScheduler _eventScheduler = null!;
    private EVStore _evStore = null!;

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
            var journey = new Journey(0, 100, new Paths([new Position(10, 10), new Position(20, 20)]));
            var ev = new EV(battery, preferences, journey, 10);
            _evStore.Set(i, ref ev);
        }
        _checkAndUpdateAllEVsHandler = new CheckAndUpdateAllEVsHandler(_eventScheduler, _evStore, 5, 10);
    }

    [Benchmark]
    public void UpdateAllEVs() => _checkAndUpdateAllEVsHandler.Handle(new CheckAndUpdateAllEVs(10));
}
