namespace Engine;

using Core.Shared;
using Engine.Events;
using Core.Helper;

/// <summary>
/// The Simulation class is responsible for running the simulation by continuously fetching and
/// handling events from the EventScheduler until a specified end time is reached.
/// </summary>
/// <param name="dispatcher">The Dispatcher that takes an Event and dispatches it to a handler.</param>
/// <param name="scheduler">The Scheduler that stores and schedules the Events.</param>
/// <param name="runUntilStop">A Time that indicates when the simulation should stop.</param>
public class Simulation(
    EventDispatcher dispatcher,
    EventScheduler scheduler,
    Time runUntilStop)
{
    /// <summary>
    /// Runs the simulation by scheduling initial events
    /// and continuously handling the next event until
    /// the specified end time is reached.
    /// </summary>
    /// <param name="cancelToken">A CancellationToken to allow graceful shutdown of the simulation.</param>
    /// <param name="waitWhilePausedAsync">A callback that blocks progress while the simulation is paused.</param>
    /// <returns>A task representing the asynchronous simulation run.</returns>
    public async Task Run(
    CancellationToken cancelToken = default,
    Func<Task>? waitWhilePausedAsync = null)
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

                    if (waitWhilePausedAsync != null)
                        await waitWhilePausedAsync();

                    var shouldContinue = await HandleNextEvent(cancelToken);
                    if (!shouldContinue)
                    {
                        Log.Info(0, 0, "Simulation finished.");
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Log.Info(0, 0, "Simulation stopped.");
                Console.WriteLine("Simulation stopped.");
            }
            catch (Exception ex)
            {
                Log.Error(0, 0, ex);
                Console.WriteLine($"Simulation crashed: {ex}");
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
