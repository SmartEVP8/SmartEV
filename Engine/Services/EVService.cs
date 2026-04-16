namespace Engine.Services;

using Core.Shared;
using Engine.Events;
using Engine.Utils;
using Engine.Vehicles;

/// <summary>
/// Service responsible for spawning EV's at each timestamp over a week.
/// </summary>
/// <param name="evPopulator">Creates EVs respecting <paramref name="distributionWindow"/>.</param>
/// <param name="scheduler">Reschedules a SpawnEV event at each handle invocation at T + <paramref name="distributionWindow"/>.</param>
/// <param name="distributionWindow">How frequently sampling is done.</param>
/// <param name="spawnFraction">A scaler controlling the total for configuration.</param>
public class EVService(
    EVPopulator evPopulator,
    EventScheduler scheduler,
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
        if (amount <= 0)
            throw LogHelper.Error(0, e.Time, new SkillissueException($"EVService was scheduled to spawn EVs at time {e.Time}, but the amount to spawn was {amount}. How did that happen?"));

        evPopulator.CreateEVs(amount, distributionWindow);
        scheduler.ScheduleEvent(new SpawnEVS(e.Time + distributionWindow));
    }
}
