namespace Engine.Events;

using Core.Shared;

/// <summary>
/// The Simulation class is responsible for running the simulation by continuously fetching and
/// handling events from the EventScheduler until a specified end time is reached.
/// </summary>
/// <param name="dispatcher">The Dispatcher that takes an Event and dispatches it to a handler.</param>
/// <param name="scheduler">The Scheduler that stores and schedules the Events.</param>
/// <param name="runUntilStop">A Time that indicates when the simulation should stop.</param>
public class Simulation(
    EventDispatcher dispatcher,
    EventScheduler scheduler, Time runUntilStop)
{
    /// <summary>
    /// Runs the simulation by scheduling initial events
    /// and continuously handling the next event until
    /// the specified end time is reached.
    /// </summary>
    /// <returns>Returns?.</returns>
    public async Task Run()
    {
        Console.WriteLine("Starting Simulation");
        scheduler.ScheduleEvent(new SpawnEVS(0));
        scheduler.ScheduleEvent(new CheckAndUpdateAllEVs(0));
        while (true)
        {
            await HandleNextEvent();
        }
    }

    /// <summary>
    /// Handles the next event in the scheduler.
    /// </summary>
    private async Task HandleNextEvent()
    {
        var nextEvent = scheduler.GetNextEvent();

        if (nextEvent != null)
        {
            if (nextEvent.Time > runUntilStop)
            {
                Console.WriteLine("Reached end of simulation time.");
                Environment.Exit(0);
            }

            await dispatcher.Dispatch(nextEvent);
        }
        else
        {
            Console.WriteLine("No more events to process.");
            Environment.Exit(0);
        }
    }
}
