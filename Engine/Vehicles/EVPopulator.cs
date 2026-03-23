namespace Engine.Vehicles;

using Core.Shared;
using Engine.Events;

public record SpawnEV(uint EVId, Time Time) : Event(EVId, Time);

public class EVPopulator(EVFactory evFactory, EVStore evStore, EventScheduler eventScheduler)
{
    private readonly EVFactory _evFactory = evFactory;
    private readonly EVStore _eVStore = evStore;
    private readonly EventScheduler _eventScheduler = eventScheduler;

    public void CreateEVs(int amount, Time distributionWindow)
    {
        var currentTime = _eventScheduler.GetCurrentTime();
        var interval = distributionWindow / amount;
        var spawnTimes = Enumerable.Range(0, amount)
                                   .Select(i => currentTime + (i * interval))
                                   .ToArray();

        Parallel.For(0, amount, i =>
        {
            var departure = (uint)spawnTimes[i];
            _eVStore.TryAllocate(amount, (index, ref ev) =>
            {
                ev = _evFactory.Create(departure);
                var spawnEvent = new SpawnEV((uint)index, departure);
                _eventScheduler.ScheduleEvent(spawnEvent);
            });
        });
    }
}
