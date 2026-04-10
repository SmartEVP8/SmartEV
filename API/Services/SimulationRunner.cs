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
    private readonly ILogger<SimulationRunner> _logger = logger;
    private Task? _simulationTask;
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Gets a value indicating whether the simulation is currently running.
    /// </summary> 
    /// <returns>True if the simulation is running; otherwise, false.</returns>
    public bool IsRunning => _simulationTask != null && !_simulationTask.IsCompleted;

    /// <summary>
    /// Starts the simulation in a background task. If the simulation is already running, this method does nothing.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task StartAsync()
    {
        if (IsRunning)
            return Task.CompletedTask;

        _cts = new CancellationTokenSource();
        _simulationTask = Task.Run(() => RunLoopAsync(_cts.Token));
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
    }

    private async Task RunLoopAsync(CancellationToken stoppingToken)
    {
        try
        {
            await simulation.Run();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("SimulationRunner stopped");
        }
    }
}
