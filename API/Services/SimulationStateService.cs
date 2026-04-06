using API.Models;

namespace API.Services;

public class SimulationStateService(ILogger<SimulationStateService> logger) : ISimulationStateService
{
    private Init? _initializationData;
    private RequestStationState? _stationState;
    private StateSnapShot? _latestSnapshot;
    private readonly List<ArriveAtStation> _arrivals = [];
    private readonly List<EndCharging> _chargingEnds = [];
    private readonly ReaderWriterLockSlim _lockObject = new();
    private readonly ILogger<SimulationStateService> _logger = logger;

    public void SetInitializationData(Init initData)
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

    public Init? GetInitializationData()
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

    public void UpdateStationState(RequestStationState stationState)
    {
        _lockObject.EnterWriteLock();
        try
        {
            _stationState = stationState;
        }
        finally
        {
            _lockObject.ExitWriteLock();
        }
    }

    public RequestStationState? GetStationState()
    {
        _lockObject.EnterReadLock();
        try
        {
            return _stationState;
        }
        finally
        {
            _lockObject.ExitReadLock();
        }
    }

    public void AddStateSnapshot(StateSnapShot snapshot)
    {
        _lockObject.EnterWriteLock();
        try
        {
            _latestSnapshot = snapshot;
        }
        finally
        {
            _lockObject.ExitWriteLock();
        }
    }

    public StateSnapShot? GetLatestSnapshot()
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

    public void RecordArrival(ArriveAtStation arrival)
    {
        _lockObject.EnterWriteLock();
        try
        {
            _arrivals.Add(arrival);
            _logger.LogDebug("EV arrived at station {StationId} at time {Time}", arrival.StationId, arrival.Time);
        }
        finally
        {
            _lockObject.ExitWriteLock();
        }
    }

    public void RecordChargingEnd(EndCharging charging)
    {
        _lockObject.EnterWriteLock();
        try
        {
            _chargingEnds.Add(charging);
            _logger.LogDebug("Charging ended at station {StationId} at time {Time}", charging.StationId, charging.Time);
        }
        finally
        {
            _lockObject.ExitWriteLock();
        }
    }

    public (List<ArriveAtStation> Arrivals, List<EndCharging> ChargingEnds) GetEvents()
    {
        _lockObject.EnterReadLock();
        try
        {
            return (new List<ArriveAtStation>(_arrivals), new List<EndCharging>(_chargingEnds));
        }
        finally
        {
            _lockObject.ExitReadLock();
        }
    }

    public void Clear()
    {
        _lockObject.EnterWriteLock();
        try
        {
            _initializationData = null;
            _stationState = null;
            _latestSnapshot = null;
            _arrivals.Clear();
            _chargingEnds.Clear();
            _logger.LogInformation("Simulation state cleared");
        }
        finally
        {
            _lockObject.ExitWriteLock();
        }
    }
}
