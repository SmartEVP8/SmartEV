using Smartev.Api.V1;

/// <summary>
/// Manages simulation state. This is a placeholder for integration with Engine/Core.
/// TODO: Wire this to the actual simulation engine.
/// </summary>
public interface ISimulationStateService
{
    void SetInitializationData(InitData initData);
    InitData? GetInitializationData();
    void UpdateSnapshot(SimulationSnapshot snapshot);
    SimulationSnapshot? GetLatestSnapshot();
}

public class SimulationStateService(ILogger<SimulationStateService> logger) : ISimulationStateService
{
    private InitData? _initializationData;
    private SimulationSnapshot? _latestSnapshot;
    private readonly ReaderWriterLockSlim _lockObject = new();
    private readonly ILogger<SimulationStateService> _logger = logger;

    public void SetInitializationData(InitData initData)
    {
        _lockObject.EnterWriteLock();
        try
        {
            _initializationData = initData;
            _logger.LogInformation("Initialization data set");
        }
        finally
        {
            _lockObject.ExitWriteLock();
        }
    }

    public InitData? GetInitializationData()
    {
        _lockObject.EnterReadLock();
        try
        {
            return _initializationData;
        }
        finally
        {
            _lockObject.ExitReadLock();
        }
    }

    public void UpdateSnapshot(SimulationSnapshot snapshot)
    {
        _lockObject.EnterWriteLock();
        try
        {
            _latestSnapshot = snapshot;
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Snapshot updated: {TotalEVs} EVs, {TotalCharging} charging",
                    snapshot.TotalEvs, snapshot.TotalCharging);
            }
        }
        finally
        {
            _lockObject.ExitWriteLock();
        }
    }

    public SimulationSnapshot? GetLatestSnapshot()
    {
        _lockObject.EnterReadLock();
        try
        {
            return _latestSnapshot;
        }
        finally
        {
            _lockObject.ExitReadLock();
        }
    }
}
