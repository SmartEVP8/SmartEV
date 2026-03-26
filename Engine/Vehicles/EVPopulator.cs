namespace Engine.Vehicles;

using Core.Shared;
using Engine.Events;

/// <summary>
/// The EVPopulator class is responsible for creating and scheduling the spawning of EVs.
/// </summary>
/// <param name="evFactory">The factory used to create new EV instances.</param>
/// <param name="evStore">The store used to allocate and manage EV instances.</param>
/// <param name="eventScheduler">The scheduler used to manage and execute events.</param>
public class EVPopulator(EVFactory evFactory, EVStore evStore, EventScheduler eventScheduler)
{
    private readonly EVFactory _evFactory = evFactory;
    private readonly EVStore _eVStore = evStore;
    private readonly EventScheduler _eventScheduler = eventScheduler;

    /// <summary>
    /// Creates a specified number of EVs and schedules their spawning events over a given distribution window.
    /// </summary>
    /// <param name="amount">The amount of EVs to create.</param>
    /// <param name="distributionWindow">The time window over which to distribute the spawning events.</param>
    public void CreateEVs(int amount, Time distributionWindow)
    {
        var currentTime = _eventScheduler.CurrentTime;
        var interval = distributionWindow / amount;
        var spawnTimes = Enumerable.Range(0, amount)
                                   .Select(i => currentTime + (i * interval))
                                   .ToArray();
        for (var i = 0; i < amount; i++)
        {
            var departure = (uint)(currentTime + (i * interval));
            _eVStore.TryAllocate(
                (index, ref ev) =>
            {
                ev = _evFactory.Create(departure);
            }, out _);
        }
    }
}
