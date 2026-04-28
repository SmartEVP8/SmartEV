namespace Engine.Vehicles;

using Core.Shared;
using Engine.Events;
using Core.Helper;
using Core.Vehicles;

/// <summary>
/// The EVPopulator class is responsible for creating and scheduling the spawning of EVs.
/// </summary>
/// <param name="evFactory">The factory used to create new EV instances.</param>
/// <param name="evs">The dictionary mapping EV IDs to EV instances.</param>
/// <param name="eventScheduler">The scheduler used to manage and execute events.</param>
public class EVPopulator(EVFactory evFactory, Dictionary<int, EV> evs, IEventScheduler eventScheduler)
{
    /// <summary>
    /// Creates a specified number of EVs and schedules their spawning events over a given distribution window.
    /// Random sampling is done sequentially upfront via <see cref="EVFactory.SampleParams"/>,
    /// after which EV construction is parallelized over the router calls.
    /// </summary>
    /// <param name="amount">The amount of EVs to create.</param>
    /// <param name="distributionWindow">The time window over which to distribute the spawning events.</param>
    public void CreateEVs(int amount, Time distributionWindow)
    {
        if (amount < 0)
            throw Log.Error(0, 0, new ArgumentException($"Amount of EVs to create cannot be negative (amount={amount})."));
        else if (amount == 0)
            return;

        var currentTime = eventScheduler.CurrentTime;
        var interval = (double)distributionWindow / amount;
        var sampledParams = evFactory.SampleParams(amount);

        var created = new EV[amount];
        Parallel.For(0, amount, i =>
        {
            var departure = (uint)(currentTime + (i * interval));
            created[i] = evFactory.Create(sampledParams[i], departure);
        });

        for (var i = 0; i < amount; i++)
        {
            var ev = created[i];
            evs.Add(ev.Id, ev);

            if (ev.CanCompleteJourney(minAcceptableCharge: ev.Preferences.MinAcceptableCharge))
            {
                ev.DriveDirectlyToDestination = true;
                var arrivalTime = ev.Journey.Original.Departure + ev.Journey.Current.Duration;
                eventScheduler.ScheduleEvent(new ArriveAtDestination(ev, arrivalTime));
            }
            else
            {
                eventScheduler.ScheduleEvent(new FindCandidateStations(ev, ev.Journey.Original.Departure));
            }
        }
    }
}
