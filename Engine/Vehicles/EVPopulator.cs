namespace Engine.Vehicles;

using Core.Shared;
using Engine.Events;
using System.Buffers;

/// <summary>
/// The EVPopulator class is responsible for creating and scheduling the spawning of EVs.
/// </summary>
/// <param name="evFactory">The factory used to create new EV instances.</param>
/// <param name="evStore">The store used to allocate and manage EV instances.</param>
/// <param name="eventScheduler">The scheduler used to manage and execute events.</param>
public class EVPopulator(EVFactory evFactory, EVStore evStore, EventScheduler eventScheduler)
{
    /// <summary>
    /// Creates a specified number of EVs and schedules their spawning events over a given distribution window.
    /// </summary>
    /// <param name="amount">The amount of EVs to create.</param>
    /// <param name="distributionWindow">The time window over which to distribute the spawning events.</param>
    public void CreateEVs(int amount, Time distributionWindow)
    {
        var currentTime = eventScheduler.CurrentTime;
        var interval = distributionWindow / amount;
        var depatures = Enumerable.Range(0, amount).Select(t => (uint)(currentTime + (t * interval))).ToArray();

        var indexes = ArrayPool<int>.Shared.Rent(amount);
        try
        {
            if (evStore.TryAllocateParallel(amount, indexes))
            {
                Parallel.For(0, amount, i =>
                {
                    evStore.Get(indexes[i]) = evFactory.Create(depatures[i]);
                });
            }
        }
        finally
        {
            foreach (var i in indexes)
            {
                ref var ev = ref evStore.Get(i);
                if (ev.CanCompleteJourney(ev.Preferences.MinAcceptableCharge))
                {
                    eventScheduler.ScheduleEvent(new ArriveAtDestination(i, currentTime + ev.Journey.OriginalDuration));
                    continue;
                }

                eventScheduler.ScheduleEvent(new CheckAndUpdateEV(i, depatures[i]));
            }

            ArrayPool<int>.Shared.Return(indexes);
        }
    }
}
