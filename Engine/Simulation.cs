namespace Engine;

using Core.Shared;
using Engine.Events;
using Core.Helper;

/// <summary>
/// The Simulation class is responsible for running the simulation by continuously fetching and
/// handling events from the EventScheduler until a specified end time is reached.
/// </summary>
/// <remarks>
/// Main constructor (full control).
/// </remarks>
public class Simulation(
    EventDispatcher dispatcher,
    EventScheduler scheduler,
    Time startFrom,
    Time runUntil)
{
    private readonly EventDispatcher _dispatcher = dispatcher;
    private readonly EventScheduler _scheduler = scheduler;
    private readonly Time _startFrom = startFrom;
    private readonly Time _runUntil = runUntil;

    /// <summary>
    /// Initializes a new instance of the <see cref="Simulation"/> class.
    /// Convenience constructor (defaults runUntil to 1 day).
    /// </summary>
    /// <param name="dispatcher">The Dispatcher that takes an Event and dispatches it to a handler.</param>
    /// <param name="scheduler">The Scheduler that stores and schedules the Events.</param>
    /// <param name="runUntil">A Time that indicates when the simulation should stop.</param>
    public Simulation(
        EventDispatcher dispatcher,
        EventScheduler scheduler,
        Time runUntil)
        : this(dispatcher, scheduler, new Time(Time.MillisecondsPerDay), runUntil)
    {
    }

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

        _scheduler.ScheduleEvent(new SpawnEVS(_startFrom));
        _scheduler.ScheduleEvent(new SnapshotEvent(_runUntil));

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
        var nextEvent = _scheduler.GetNextEvent();

        if (nextEvent != null)
        {
            if (nextEvent.Time > _runUntil)
            {
                Console.WriteLine("Reached end of simulation time.");
                return false;
            }

            await _dispatcher.Dispatch(nextEvent);
            return true;
        }
        else
        {
            Console.WriteLine("No more events to process.");
            return false;
        }
    }
}
