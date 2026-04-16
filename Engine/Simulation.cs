namespace Engine;

using Core.Shared;
using Engine.Events;

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
    /// <param name="cancelToken">A CancellationToken to allow graceful shutdown of the simulation.</param>
    /// <returns>Returns?.</returns>
    public async Task Run(CancellationToken cancelToken = default)
    {
        Log.Info(0, 0, "Simulation started.");
        Console.WriteLine("Starting Simulation");

        scheduler.ScheduleEvent(new SpawnEVS(0));
        scheduler.ScheduleEvent(new SnapshotEvent(0));

        try
        {
            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();
                var shouldContinue = await HandleNextEvent(cancelToken);
                if (!shouldContinue)
                {
                    Log.Info(0, 0, "Simulation finished.");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn(0, 0, "Simulation crashed.");
            Console.WriteLine($"Simulation crashed: {ex}");
        }
        finally
        {
            await Serilog.Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Handles the next event in the scheduler.
    /// </summary>
    private async Task<bool> HandleNextEvent(CancellationToken cancelToken)
    {
        cancelToken.ThrowIfCancellationRequested();
        var nextEvent = scheduler.GetNextEvent();

        if (nextEvent != null)
        {
            if (nextEvent.Time > runUntilStop)
            {
                Console.WriteLine("Reached end of simulation time.");
                return false;
            }

            await dispatcher.Dispatch(nextEvent);
            return true;
        }
        else
        {
            Console.WriteLine("No more events to process.");
            return false;
        }
    }
}
