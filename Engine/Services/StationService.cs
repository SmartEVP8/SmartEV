namespace Engine.Services;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Charging.ChargingModel.Chargepoint;
using Core.Shared;
using Engine.Events;
using Engine.Vehicles;
using Engine.Routing;
using Engine.Utils;
using Engine.Metrics;
using Engine.Metrics.Events;
using Engine.Metrics.Snapshots;
using Engine.Init;

/// <summary>
/// Tracks an active charging session at one side of a charger.
/// </summary>
public record ChargingSession(
    int EVId,
    ConnectedEV EV,
    Time StartTime,
    ChargingSide? Side,  // null for single chargers
    uint? EndChargingCancellationToken // Gets set after scheduling
);

/// <summary>
/// Tracks the runtime state of a charger, active sessions, waiting queue, and last integration result.
/// </summary>
public class ChargerState(ChargerBase charger, ushort stationId)
{
    /// <summary>
    /// Gets charger this state belongs to.
    /// </summary>
    public ChargerBase Charger { get; } = charger;

    /// <summary>
    /// Gets the id of the station this charger belongs to for metrics tagging.
    /// </summary>
    public ushort StationId { get; } = stationId;

    /// <summary>
    /// Gets the queue of EVs waiting to charge at this charger, in order of arrival.
    /// </summary>
    public Queue<(int EVId, ConnectedEV EV)> Queue { get; } = new();

    /// <summary>
    /// Gets or sets the active charging session at side A, or null if free. Always used for single chargers.
    /// </summary>
    public ChargingSession? SessionA { get; set; }

    /// <summary>
    /// Gets or sets The id used for cancellation of the EndChargingEvent.
    /// </summary>
    public uint CancellationToken { get; set; }

    /// <summary>
    /// Gets or sets the active charging session at side B, or null if free. Always null for single chargers.
    /// </summary>
    public ChargingSession? SessionB { get; set; }

    /// <summary>
    /// Gets or sets the result of the last integration run for the charger.
    /// </summary>
    public IntegrationResult? LastResult { get; set; }

    /// <summary>
    /// Gets or sets the maximum queue size seen during the current snapshot window.
    /// </summary>
    public int WindowMaxQueueSize { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this charger had any activity during the current snapshot window.
    /// </summary>
    public bool WindowHadActivity { get; set; }

    /// <summary>
    /// Gets or sets the energy delivered in the current snapshot window in kWh.
    /// </summary>
    public double WindowDeliveredKWh { get; set; }

    /// <summary>
    /// Gets or sets the last simulation time the energy accumulator was updated.
    /// </summary>
    public Time LastEnergyUpdateTime { get; set; }

    /// <summary>
    /// Gets a value indicating whether the charger has at least one free side.
    /// </summary>
    public bool IsFree => Charger switch
    {
        SingleCharger => SessionA is null,
        DualCharger => SessionA is null || SessionB is null,
        _ => false
    };

    /// <summary>
    /// Accumulates exact energy using the pre-calculated charging curve trajectory.
    /// </summary>
    public void AccumulateEnergy(Time simNow)
    {
        if (simNow <= LastEnergyUpdateTime || LastResult is null) return;

        if (SessionA is not null)
        {
            WindowDeliveredKWh += GetEnergyFromCurve(
                LastResult.CumulativeEnergyA, LastResult.StepSeconds, SessionA.StartTime, LastEnergyUpdateTime, simNow);
        }

        if (SessionB is not null)
        {
            WindowDeliveredKWh += GetEnergyFromCurve(
                LastResult.CumulativeEnergyB, LastResult.StepSeconds, SessionB.StartTime, LastEnergyUpdateTime, simNow);
        }

        LastEnergyUpdateTime = simNow;
    }

    private double GetEnergyFromCurve(List<double> curve, uint stepSeconds, Time sessionStart, Time lastUpdate, Time simNow)
    {
        if (curve.Count == 0) return 0.0;

        // Cast to 'long' first to prevent uint underflow. 
        // If lastUpdate is before sessionStart, it goes negative, and Math.Max clamps it to 0.
        var startOffset = Math.Max(0L, (long)lastUpdate - (long)sessionStart);
        var endOffset = Math.Max(0L, (long)simNow - (long)sessionStart);

        var startIndex = Math.Min(curve.Count - 1, (int)(startOffset / stepSeconds));
        var endIndex = Math.Min(curve.Count - 1, (int)(endOffset / stepSeconds));

        return curve[endIndex] - curve[startIndex];
    }
}

/// <summary>
/// Service responsible for managing the state of stations and chargers, handling events related to reservations, arrivals, and charging sessions.
/// </summary>
public class StationService : IStationService
{
    private readonly Dictionary<ushort, List<ChargerState>> _stationChargers = [];
    private readonly Dictionary<int, ChargerState> _chargerIndex = [];
    private readonly Dictionary<ushort, Station> _stationIndex = [];
    private readonly Dictionary<ushort, uint> _windowReservations = [];
    private readonly Dictionary<ushort, uint> _windowCancellations = [];
    private readonly ChargingIntegrator _integrator;
    private readonly EventScheduler _scheduler;
    private readonly EVStore _eVStore;
    private readonly ApplyNewPath _applyNewPath;
    private readonly MetricsService _metrics;
    private readonly EngineSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="StationService"/> class.
    /// </summary>
    /// <param name="stations">The collection of stations to manage.</param>
    /// <param name="integrator">The charging integrator to use for simulating charging sessions.</param>
    /// <param name="scheduler">The event scheduler to use for scheduling future events.</param>
    /// <param name="evStore">The storage of current EV's.</param>
    /// <param name="applyNewPath">The path deviator to use for calculating route deviations through charging stations.</param>
    /// <param name="metrics">The metrics service to use for recording metrics.</param>
    /// <param name="settings">The engine settings containing configuration options.</param>
    public StationService(
        ICollection<Station> stations,
        ChargingIntegrator integrator,
        EventScheduler scheduler,
        EVStore evStore,
        ApplyNewPath applyNewPath,
        MetricsService metrics,
        EngineSettings settings)
    {
        _integrator = integrator;
        _scheduler = scheduler;
        _eVStore = evStore;
        _applyNewPath = applyNewPath;
        _metrics = metrics;
        _settings = settings;
        foreach (var station in stations)
        {
            _stationIndex[station.Id] = station;
            var states = station.Chargers.Select(c => new ChargerState(c, station.Id)).ToList();
            _stationChargers[station.Id] = states;
            foreach (var cs in states)
                _chargerIndex[cs.Charger.Id] = cs;
        }
    }

    /// <summary>
    /// Returns the charger state for the given charger id.
    /// </summary>
    /// <param name="chargerId">The id of the charger.</param>
    /// <returns>The charger state for the given charger id.</returns>
    public ChargerState? GetChargerState(int chargerId)
        => _chargerIndex.TryGetValue(chargerId, out var state) ? state : null;

    /// <inheritdoc/>
    public Station? GetStation(ushort stationId)
        => _stationIndex.TryGetValue(stationId, out var station) ? station : null;

    /// <inheritdoc/>
    public int GetTotalQueueSize(ushort stationId)
    {
        if (!_stationChargers.TryGetValue(stationId, out var chargers))
            return 0;
        return chargers.Sum(cs => cs.Queue.Count);
    }

    /// <summary>
    /// Collects charger snapshots from runtime charger states for the provided simulation time.
    /// Includes per-charger utilization computed from the latest integrator result.
    /// </summary>
    /// <param name="simNow">The simulation time of the snapshot.</param>
    /// <returns>All charger snapshots across all stations.</returns>
    public (IEnumerable<ChargerSnapshotMetric> Chargers, IEnumerable<StationSnapshotMetric> Stations) CollectAllSnapshots(Time simNow)
    {
        var chargerMetrics = new List<ChargerSnapshotMetric>();
        var stationMetrics = new List<StationSnapshotMetric>();

        foreach (var (stationId, chargerStates) in _stationChargers)
        {
            var station = _stationIndex[stationId];
            var totalDeliveredKW = 0f;
            var totalMaxKW = 0f;
            var totalQueueSize = 0;

            foreach (var state in chargerStates)
            {
                state.AccumulateEnergy(simNow);
                var deliveredKWhInWindow = (float)state.WindowDeliveredKWh;
                var targetEVDemandKWh = 0f;
                if (state.SessionA is not null)
                {
                    var ev = state.SessionA.EV;
                    targetEVDemandKWh += (float)Math.Max(0, (ev.TargetSoC - ev.CurrentSoC) * ev.CapacityKWh);
                }

                if (state.SessionB is not null)
                {
                    var ev = state.SessionB.EV;
                    targetEVDemandKWh += (float)Math.Max(0, (ev.TargetSoC - ev.CurrentSoC) * ev.CapacityKWh);
                }

                var snapshotDurationHours = _settings.SnapshotInterval / 3600f;
                var maxPossibleKWh = state.Charger.MaxPowerKW * snapshotDurationHours;
                var utilizationInWindow = maxPossibleKWh > 0
                    ? Math.Clamp(deliveredKWhInWindow / maxPossibleKWh, 0f, 1f)
                    : 0f;

                var queueSizeInWindow = Math.Max(state.WindowMaxQueueSize, state.Queue.Count);

                chargerMetrics.Add(ChargerSnapshotMetric.Collect(
                    state.Charger,
                    stationId,
                    simNow,
                    queueSizeInWindow,
                    utilizationInWindow,
                    deliveredKWhInWindow,
                    state.Charger is DualCharger,
                    targetEVDemandKWh));

                totalDeliveredKW += deliveredKWhInWindow;
                totalMaxKW += state.Charger.MaxPowerKW;
                totalQueueSize += queueSizeInWindow;

                state.WindowHadActivity = false;
                state.WindowMaxQueueSize = state.Queue.Count;
                state.WindowDeliveredKWh = 0;
            }

            _windowReservations.TryGetValue(stationId, out var reservations);
            _windowCancellations.TryGetValue(stationId, out var cancellations);

            _windowReservations[stationId] = 0;
            _windowCancellations[stationId] = 0;

            stationMetrics.Add(new StationSnapshotMetric
            {
                SimTime = (uint)simNow,
                StationId = stationId,
                TotalDeliveredKW = totalDeliveredKW,
                TotalMaxKW = totalMaxKW,
                TotalQueueSize = totalQueueSize,
                Price = station.GetPrice(simNow),
                TotalChargers = chargerStates.Count,
                Reservations = reservations,
                Cancellations = cancellations,
            });
        }

        return (chargerMetrics, stationMetrics);
    }

    /// <summary>
    /// Handles a reservation request from an EV to a station.
    /// If the EV already has an active reservation, the existing arrival event is cancelled before proceeding.
    /// Calculates the detoured path through the station, updates the EV's journey, and schedules a new
    /// arrival event.
    /// </summary>
    /// <param name="e">The reservation request event.</param>
    public void HandleReservationRequest(ReservationRequest e)
    {
        ref var ev = ref _eVStore.Get(e.EVId);
        if (!_stationIndex.TryGetValue(e.StationId, out var station))
            return;

        if (ev.HasReservationAtStationId != null)
        {
            if (ev.HasReservationAtStationId.Value == e.StationId)
            {
                // Already has a reservation at this station, no need to cancel and re-reserve
                return;
            }

            _scheduler.ScheduleEvent(new CancelRequest(e.EVId, ev.HasReservationAtStationId.Value, e.Time));
        }

        _windowReservations[e.StationId]++;
        ev.HasReservationAtStationId = e.StationId;
        _applyNewPath.ApplyNewPathToEV(ref ev, station, e.Time);
        _scheduler.ScheduleEvent(
            new ArriveAtStation(e.EVId, e.StationId, ev.CalcDesiredSoC(e.Time + e.DurationToStation), e.Time + e.DurationToStation));
    }

    /// <summary>
    /// Handles a cancellation request from an EV, decrementing the station's active reservation count,
    /// clearing the reservation from the EV, and cancelling the scheduled arrival event.
    /// </summary>
    /// <param name="e">The cancellation request event.</param>
    public void HandleCancelRequest(CancelRequest e)
    {
        ref var ev = ref _eVStore.Get(e.EVId);
        if (!_stationIndex.TryGetValue(e.StationId, out var station))
            return;

        _windowCancellations[e.StationId]++;

        if (ev.HasReservationAtStationId != null)
        {
            _scheduler.CancelEvent((uint)ev.HasReservationAtStationId);
        }
        else
        {
            throw new SkillissueException("Should never cancel without a reservation cancellation token");
        }

        ev.HasReservationAtStationId = null;
    }

    /// <summary>
    /// Called when an EV arrives at a station.
    /// Finds the best compatible charger, joins its queue, and starts charging only if a side is free.
    /// </summary>
    /// <param name="e">The arrival event.</param>
    public void HandleArrivalAtStation(ArriveAtStation e)
    {
        var ev = _eVStore.Get(e.EVId);
        if (!_stationChargers.TryGetValue(e.StationId, out var chargers))
            return;

        var target = chargers
            .Where(cs => cs.Charger.GetSockets().Contains(ev.Battery.Socket))
            .OrderBy(cs => cs.IsFree ? 0 : 1)
            .ThenBy(cs => cs.Queue.Count)
            .FirstOrDefault();

        if (target is null)
            return;

        var connectedEV = new ConnectedEV(
                EVId: e.EVId,
                CurrentSoC: ev.Battery.StateOfCharge,
                TargetSoC: e.TargetSoC,
                CapacityKWh: ev.Battery.MaxCapacityKWh,
                MaxChargeRateKW: ev.Battery.MaxChargeRateKW,
                Socket: ev.Battery.Socket,
                ArrivalTime: e.Time);

        ev.IsCharging = true;
        target.Queue.Enqueue((e.EVId, connectedEV));
        UpdateWindowStats(target);
        if (target.IsFree)
            StartCharging(target, e.Time);
    }

    /// <summary>
    /// Called when a charging session ends for a specific EV.
    /// Uses the internally stored IntegrationResult to update remaining car SoC.
    /// </summary>
    /// <param name="e">The EndCharging event containing the EVId, ChargerId, and Time of the event.</param>
    /// <summary>
    /// Called when a charging session ends for a specific EV.
    /// </summary>
    public void HandleEndCharging(EndCharging e)
    {
        if (!_chargerIndex.TryGetValue(e.ChargerId, out var state))
            return;

        state.AccumulateEnergy(e.Time);
        var result = state.LastResult;
        state.LastResult = null;
        ref var ev = ref _eVStore.Get(e.EVId);
        switch (state.Charger)
        {
            case SingleCharger single:
                single.ChargingPoint.Disconnect();

                state.SessionA = null;
                ev.IsCharging = false;
                ev.HasReservationAtStationId = null;
                break;

            case DualCharger dual:
                if (state.SessionA?.EVId == e.EVId)
                {
                    dual.ChargingPoint.Disconnect(ChargingSide.Left);
                    state.SessionA = null;
                    ev.IsCharging = false;
                    ev.HasReservationAtStationId = null;

                    if (state.SessionB is not null && result is not null)
                    {
                        var updatedSoC = result.BSoCWhenAFinish;
                        state.SessionB = state.SessionB with
                        {
                            EV = state.SessionB.EV with { CurrentSoC = updatedSoC }
                        };

                        if (state.SessionB!.EndChargingCancellationToken is { } token)
                            _scheduler.CancelEvent(token);

                        if (updatedSoC >= state.SessionB.EV.TargetSoC)
                        {
                            dual.ChargingPoint.Disconnect(ChargingSide.Right);
                            _eVStore.Get(state.SessionB.EVId).IsCharging = false;
                            state.SessionB = null;
                        }
                    }
                }
                else if (state.SessionB?.EVId == e.EVId)
                {
                    dual.ChargingPoint.Disconnect(ChargingSide.Right);
                    state.SessionB = null;
                    ev.IsCharging = false;
                    ev.HasReservationAtStationId = null;

                    if (state.SessionA is not null && result is not null)
                    {
                        var updatedSoC = result.ASoCWhenBFinish;
                        state.SessionA = state.SessionA with
                        {
                            EV = state.SessionA.EV with { CurrentSoC = updatedSoC }
                        };

                        if (state.SessionA!.EndChargingCancellationToken is { } token)
                            _scheduler.CancelEvent(token);

                        if (updatedSoC >= state.SessionA.EV.TargetSoC)
                        {
                            dual.ChargingPoint.Disconnect(ChargingSide.Left);
                            _eVStore.Get(state.SessionA.EVId).IsCharging = false;
                            state.SessionA = null;
                        }
                    }
                }

                break;
        }

        StartCharging(state, e.Time);
    }

    private void StartCharging(ChargerState state, Time simNow)
    {
        if (_integrator == null) return;

        state.AccumulateEnergy(simNow);

        switch (state.Charger)
        {
            case SingleCharger single:
                if (state.SessionA is not null) break;
                if (!state.Queue.TryPeek(out var next)) break;

                if (!single.ChargingPoint.TryConnect(next.EV.Socket))
                {
                    throw new InvalidOperationException(
                        $"Logic Error: EV {next.EVId} reached Charger {single.Id} but TryConnect failed. " +
                        "Check if HandleEndCharging is properly calling Disconnect() before StartCharging.");
                }

                state.Queue.Dequeue();

                _metrics.RecordWaitTime(new EVWaitTimeMetric
                {
                    EVId = next.EVId,
                    StationId = state.StationId,
                    ArrivalAtStationTime = next.EV.ArrivalTime,
                    StartChargingTime = simNow,
                });

                state.SessionA = new ChargingSession(next.EVId, next.EV, simNow, null, null);
                _eVStore.Get(next.EVId).IsCharging = true; // Mark as charging

                var result = _integrator.IntegrateSingleToCompletion(
                    simNow, single.MaxPowerKW, single.ChargingPoint, state.SessionA.EV);
                state.LastResult = result;

                if (result?.FinishTimeA is not null)
                {
                    var token = _scheduler.ScheduleEvent(new EndCharging(next.EVId, single.Id, result.FinishTimeA.Value));
                    state.SessionA = state.SessionA with { EndChargingCancellationToken = token };
                }

                break;

            case DualCharger dual:
                var wasAloneA = state.SessionA is not null && state.SessionB is null;
                var wasAloneB = state.SessionB is not null && state.SessionA is null;
                var hadBothBefore = state.SessionA is not null && state.SessionB is not null;

                while (state.Queue.TryPeek(out var candidate))
                {
                    var side = dual.ChargingPoint.TryConnect(candidate.EV.Socket);
                    if (side is null) break;

                    state.Queue.Dequeue();

                    _metrics.RecordWaitTime(new EVWaitTimeMetric
                    {
                        EVId = candidate.EVId,
                        StationId = state.StationId,
                        ArrivalAtStationTime = candidate.EV.ArrivalTime,
                        StartChargingTime = simNow,
                    });

                    var session = new ChargingSession(candidate.EVId, candidate.EV, simNow, side, null);
                    _eVStore.Get(candidate.EVId).IsCharging = true;

                    if (side == ChargingSide.Left) state.SessionA = session;
                    else state.SessionB = session;
                }

                if (state.SessionA is null && state.SessionB is null && state.Queue.Count > 0)
                {
                    var (eVId, eV) = state.Queue.Peek();
                    throw new InvalidOperationException(
                        $"Logic Error: DualCharger {dual.Id} is empty, but failed to connect EV {eVId}. " +
                        $"Car Socket: {eV.Socket}. Check if Disconnect() was called in HandleEndCharging.");
                }

                var nowHasBoth = state.SessionA is not null && state.SessionB is not null;
                if (!hadBothBefore && nowHasBoth)
                {
                    if (wasAloneA && state.SessionA?.EndChargingCancellationToken is { } tA) _scheduler.CancelEvent(tA);
                    if (wasAloneB && state.SessionB?.EndChargingCancellationToken is { } tB) _scheduler.CancelEvent(tB);
                }

                if (state.SessionA is null && state.SessionB is null) break;

                var carA = state.SessionA?.EV ?? state.SessionB!.EV with { CurrentSoC = state.SessionB.EV.TargetSoC };
                var carB = state.SessionB?.EV ?? state.SessionA!.EV with { CurrentSoC = state.SessionA.EV.TargetSoC };

                var dualResult = _integrator.IntegrateDualToCompletion(
                    simNow, dual.MaxPowerKW, dual.ChargingPoint, carA, carB);

                state.LastResult = dualResult;

                if (state.SessionA is not null && dualResult?.FinishTimeA is not null)
                {
                    var token = _scheduler.ScheduleEvent(new EndCharging(state.SessionA.EVId, dual.Id, dualResult.FinishTimeA.Value));
                    state.SessionA = state.SessionA with { EndChargingCancellationToken = token };
                }

                if (state.SessionB is not null && dualResult?.FinishTimeB is not null)
                {
                    var token = _scheduler.ScheduleEvent(new EndCharging(state.SessionB.EVId, dual.Id, dualResult.FinishTimeB.Value));
                    state.SessionB = state.SessionB with { EndChargingCancellationToken = token };
                }

                break;
        }

        UpdateWindowStats(state);
    }

    private static void UpdateWindowStats(ChargerState state)
    {
        var hasActivity = state.Queue.Count > 0
            || state.SessionA is not null
            || state.SessionB is not null
            || state.WindowDeliveredKWh > 0;

        if (!hasActivity)
            return;

        state.WindowHadActivity = true;
        state.WindowMaxQueueSize = Math.Max(state.WindowMaxQueueSize, state.Queue.Count);
    }
}
