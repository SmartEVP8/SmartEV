using Protocol;

namespace API.Services;

public partial class SimulationStateService(ILogger<SimulationStateService> logger) : ISimulationStateService
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
            LogSnapshotUpdated(snapshot.TotalEvs, snapshot.TotalCharging);
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

    [LoggerMessage(Level = LogLevel.Debug, Message = "Snapshot updated: {TotalEVs} EVs, {TotalCharging} charging")]
    private partial void LogSnapshotUpdated(uint totalEvs, uint totalCharging);
}
