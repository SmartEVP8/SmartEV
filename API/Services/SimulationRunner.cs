namespace API.Services;

using Core.Helper;

using Engine;

/// <summary>
/// Manages the execution of the simulation in a background task, allowing it to run concurrently with the API server.
/// </summary>
/// <param name="simulation">The simulation instance to run.</param>
public sealed class SimulationRunner(
    Simulation simulation)
{
    private Task? _simulationTask;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Gets a value indicating whether the simulation is currently running.
    /// </summary>
    /// <returns>True if the simulation is running; otherwise, false.</returns>
    public bool IsRunning => _simulationTask?.IsCompleted is false;

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
        _simulationTask = Task.Run(() => RunLoopAsync(_cts.Token), _cts.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the simulation by signaling cancellation and awaiting the completion of the simulation task. If the simulation is not running, this method does nothing.
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
    }

    private async Task RunLoopAsync(CancellationToken cancelToken)
    {
        try
        {
            await simulation.Run(cancelToken);
        }
        catch (OperationCanceledException)
        {
            Log.Info(0, 0, "SimulationRunner stopped");
        }
        catch (Exception ex)
        {
            Log.Error(0, 0, ex);
        }
    }
}
