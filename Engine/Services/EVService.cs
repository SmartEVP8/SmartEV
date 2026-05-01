namespace Engine.Services;

using Core.Shared;
using Engine.Events;
using Engine.Grid;
using Engine.Spawning;
using Engine.Utils;
using Engine.Vehicles;
using Serilog;

/// <summary>
/// Service responsible for spawning EV's at each timestamp over a week.
/// </summary>
/// <param name="evPopulator">Creates EVs respecting <paramref name="distributionWindow"/>.</param>
/// <param name="scheduler">Reschedules a SpawnEV event at each handle invocation at T + <paramref name="distributionWindow"/>.</param>
/// <param name="distributionWindow">How frequently sampling is done.</param>
/// <param name="journeySampler">Provides a journey sampler used to assign journeys to spawned EVs.</param>
/// <param name="spawnFraction">A scaler controlling the total for configuration.</param>
public class EVService(
    EVPopulator evPopulator,
    EventScheduler scheduler,
    Time distributionWindow,
    IJourneySamplerProvider journeySampler,
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
        journeySampler.SetCurrent(e.Time);
        if (amount <= 0)
        {
            Log.Error("EVService was scheduled to spawn EVs at time {@Time}, but the amount to spawn was {Amount}.", e.Time, amount);
            throw new SkillissueException($"EVService was scheduled to spawn EVs at time {e.Time}, but the amount to spawn was {amount}.");
        }

        evPopulator.CreateEVs(amount, distributionWindow);
        scheduler.ScheduleEvent(new SpawnEVS(e.Time + distributionWindow));
    }
}
