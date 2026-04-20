namespace API.Services;

using Engine;

/// <summary>
/// Manages the execution of the simulation in a background task, allowing it to run concurrently with the API server.
/// </summary>
/// <param name="simulation">The simulation instance to run.</param>
/// <param name="logger">The logger for recording events and errors.</param>
public sealed class SimulationRunner(
    Simulation simulation,
    ILogger<SimulationRunner> logger)
{
    private Task? _simulationTask;
    private CancellationTokenSource? _cts;
    private volatile bool _isPaused;

    /// <summary>
    /// Gets a value indicating whether the simulation is currently running.
    /// </summary>
    /// <returns>True if the simulation is running; otherwise, false.</returns>
    public bool IsRunning => _simulationTask != null && !_simulationTask.IsCompleted;

    /// <summary>
    /// Gets a value indicating whether the simulation is currently paused.
    /// </summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Starts the simulation in a background task. If the simulation is already running, this method does nothing.
    /// </summary>
    /// <param name="cancelToken">Cancellation token that will stop the simulation loop.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StartAsync(CancellationToken cancelToken = default)
    {
        if (IsRunning)
            return Task.CompletedTask;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        _isPaused = false;
        _simulationTask = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the simulation by signalling cancellation and awaiting the completion of the simulation task.
    /// If the simulation is not running, this method does nothing.
    /// </summary>
    /// <returns>A task representing the asynchronous operation that stops the simulation.</returns>
    public async Task StopAsync()
    {
        if (!IsRunning)
            return;

        _cts?.Cancel();

        if (_simulationTask != null)
            await _simulationTask;

        _cts?.Dispose();
        _cts = null;
        _simulationTask = null;
        _isPaused = false;
    }

    /// <summary>
    /// Pauses the simulation if it is currently running.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task PauseAsync()
    {
        if (!IsRunning)
            return Task.CompletedTask;

        _isPaused = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Resumes the simulation if it is currently running.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ResumeAsync()
    {
        if (!IsRunning)
            return Task.CompletedTask;

        _isPaused = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Waits while the simulation is paused.
    /// </summary>
    /// <param name="cancelToken">Cancellation token that stops the wait.</param>
    /// <returns>A task representing the asynchronous wait operation.</returns>
    private async Task WaitWhilePausedAsync(CancellationToken cancelToken)
    {
        while (_isPaused && !cancelToken.IsCancellationRequested)
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
            logger.LogInformation("SimulationRunner stopped");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in SimulationRunner");
        }
    }
}