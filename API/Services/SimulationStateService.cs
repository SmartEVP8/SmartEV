namespace API.Services;

using Protocol;

/// <summary>
/// Manages simulation state with simple volatile fields for single-client sequential model.
/// </summary>
public partial class SimulationStateService(ILogger<SimulationStateService> logger)
{
    private volatile InitData? _initializationData;
    private volatile SimulationSnapshot? _latestSnapshot;
    private readonly ILogger<SimulationStateService> _logger = logger;

    public void SetInitializationData(InitData initData)
    {
        _initializationData = initData;
        _logger.LogInformation("Initialization data set");
    }

    public InitData? GetInitializationData()
    {
        return _initializationData;
    }

    public void UpdateSnapshot(SimulationSnapshot snapshot)
    {
        _latestSnapshot = snapshot;
        LogSnapshotUpdated(snapshot.TotalEvs, snapshot.TotalCharging);
    }

    public SimulationSnapshot? GetLatestSnapshot()
    {
        return _latestSnapshot;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Snapshot updated: {TotalEVs} EVs, {TotalCharging} charging")]
    private partial void LogSnapshotUpdated(uint totalEvs, uint totalCharging);
}
