namespace Engine.Services;

using Core.Shared;
using Engine.Events;
using Engine.Vehicles;

/// <summary>
/// Service responsible for spawning EV's at each timestamp over a week.
/// </summary>
/// <param name="evPopulator">Creates EVs respecting <paramref name="distributionWindow"/>.</param>
/// <param name="scheduler">Reschedules a SpawnEV event at each handle invocation at T + <paramref name="distributionWindow"/>.</param>
/// <param name="distributionWindow">How frequently sampling is done.</param>
/// <param name="spawnFraction">A scaler controlling the total for configuration.</param>
/// <param name="evStore">The store for EVs, used for logging purposes.</param>
public class EVService(
    EVPopulator evPopulator,
    EventScheduler scheduler,
    EVStore evStore,
    Time distributionWindow,
    double spawnFraction)
{
    private readonly CarsInPeriod _carsInPeriod = new(distributionWindow, spawnFraction);

    /// <summary>
    /// Spawns the an amount of EV's at each timestamp over a week.
    /// </summary>
    /// <param name="e">The spawn event.</param>
    public void Handle(SpawnEVS e)
    {
        var amount = _carsInPeriod.GetCarsInPeriod(e.Time);
        evPopulator.CreateEVs(amount, distributionWindow);
        scheduler.ScheduleEvent(new SpawnEVS(e.Time + distributionWindow));

        var inSystem = evStore.Count - evStore.AvailableCapacity();
        Console.WriteLine($"[SpawnEVS] T={e.Time} | Spawned: {amount} | EVs in system: {inSystem} / {evStore.Count}");
    }
}
