namespace API.Services;

using Core.Helper;

using Engine;

/// <summary>
/// Represents the current execution state of the simulation.
/// </summary>
public enum SimulationState
{
    Stopped,
    Running,
    Paused,
}

/// <summary>
/// Manages the execution of the simulation in a background task, allowing it to run concurrently with the API server.
/// </summary>
/// <param name="simulation">The simulation instance to run.</param>
public sealed class SimulationRunner(
    Simulation simulation)
{
    private Task? _simulationTask;
    private CancellationTokenSource? _cts;
    private volatile int _state = (int)SimulationState.Stopped;

    /// <summary>
    /// Gets the current state of the simulation.
    /// </summary>
    public SimulationState State => (SimulationState)_state;

    /// <summary>
    /// Starts the simulation in a background task. If the simulation is not stopped, this method does nothing.
    /// </summary>
    /// <param name="cancelToken">Cancellation token that will stop the simulation loop.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StartAsync(CancellationToken cancelToken = default)
    {
        if (State != SimulationState.Stopped)
            return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        _state = (int)SimulationState.Running;
        _simulationTask = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the simulation by signalling cancellation and awaiting the completion of the simulation task.
    /// If the simulation is already stopped, this method does nothing.
    /// </summary>
    /// <returns>A task representing the asynchronous operation that stops the simulation.</returns>
    public async Task StopAsync()
    {
        if (State == SimulationState.Stopped)
            return;

        _cts?.Cancel();

        if (_simulationTask != null)
            await _simulationTask;
    }

    /// <summary>
    /// Pauses the simulation if it is currently running.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task PauseAsync()
    {
        if (State != SimulationState.Running)
            return Task.CompletedTask;

        _state = (int)SimulationState.Paused;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Resumes the simulation if it is currently paused.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ResumeAsync()
    {
        if (State != SimulationState.Paused)
            return Task.CompletedTask;

        _state = (int)SimulationState.Running;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Waits while the simulation is paused.
    /// </summary>
    /// <param name="cancelToken">Cancellation token that stops the wait.</param>
    /// <returns>A task representing the asynchronous wait operation.</returns>
    private async Task WaitWhilePausedAsync(CancellationToken cancelToken)
    {
        while (State == SimulationState.Paused && !cancelToken.IsCancellationRequested)
            await Task.Delay(100, cancelToken);
    }

    private async Task RunLoopAsync(CancellationToken cancelToken)
    {
        try
        {
            await simulation.Run(cancelToken, () => WaitWhilePausedAsync(cancelToken));
        }
        catch (OperationCanceledException)
        {
            Log.Info(0, 0, "SimulationRunner stopped");
        }
        catch (Exception ex)
        {
            Log.Error(0, 0, ex, ("message", "Error in SimulationRunner"));
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            _simulationTask = null;
            _state = (int)SimulationState.Stopped;
        }
    }
}