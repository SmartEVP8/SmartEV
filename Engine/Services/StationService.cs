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
    private readonly Time _snapshotInterval;
    private readonly bool _bypassArrivalHandling;
    private readonly StationMetricsCollector _metricsCollector;

    /// <summary>
    /// Initializes a new instance of the <see cref="StationService"/> class.
    /// </summary>
    /// <param name="stations">The collection of stations to manage.</param>
    /// <param name="integrator">The charging integrator to use for simulating charging sessions.</param>
    /// <param name="scheduler">The event scheduler to use for scheduling future events.</param>
    /// <param name="evStore">The storage of current EV's.</param>
    /// <param name="applyNewPath">The path deviator to use for calculating route deviations through charging stations.</param>
    /// <param name="metrics">The metrics service to use for recording metrics.</param>
    /// <param name="snapshotInterval">The interval at which to collect snapshots.</param>
    /// <param name="bypassArrivalHandling">If true, arriving EVs are freed immediately instead of entering charger queues.</param>
    public StationService(
        ICollection<Station> stations,
        ChargingIntegrator integrator,
        EventScheduler scheduler,
        EVStore evStore,
        ApplyNewPath applyNewPath,
        MetricsService metrics,
        Time snapshotInterval,
        bool bypassArrivalHandling = false)
    {
        _integrator = integrator;
        _scheduler = scheduler;
        _eVStore = evStore;
        _applyNewPath = applyNewPath;
        _metrics = metrics;
        _snapshotInterval = snapshotInterval;
        _bypassArrivalHandling = bypassArrivalHandling;
        _metricsCollector = new StationMetricsCollector(snapshotInterval);

        foreach (var station in stations)
        {
            _stationIndex[station.Id] = station;
            _windowReservations[station.Id] = 0;
            _windowCancellations[station.Id] = 0;
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

    /// <summary>
    /// Gets all stations managed by this service.
    /// </summary>
    /// <returns>A collection of all stations.</returns>
    public ICollection<Station> GetAllStations()
        => _stationIndex.Values;

    /// <inheritdoc/>
    public int GetTotalQueueSize(ushort stationId)
    {
        if (!_stationChargers.TryGetValue(stationId, out var chargers))
            return 0;
        return chargers.Sum(cs => cs.Queue.Count);
    }

    /// <summary>
    /// Collects all snapshots for the given simulation time.
    /// </summary>
    /// <param name="simNow">The current simulation time.</param>
    /// <returns>A tuple containing the charger and station snapshots.</returns>
    public (IEnumerable<ChargerSnapshotMetric> Chargers, IEnumerable<StationSnapshotMetric> Stations) CollectAllSnapshots(Time simNow)
    {
        // Delegate all the heavy lifting to the collector class
        return _metricsCollector.Collect(
            simNow,
            _stationChargers,
            _stationIndex,
            _windowReservations,
            _windowCancellations);
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
                return;

            _scheduler.ScheduleEvent(new CancelRequest(e.EVId, ev.HasReservationAtStationId.Value, e.Time));
        }

        _windowReservations[e.StationId] = _windowReservations.GetValueOrDefault(e.StationId) + 1;
        station.IncrementReservations();

        _applyNewPath.ApplyNewPathToEV(ref ev, station, e.Time);
        ev.HasReservationAtStationId = e.StationId;

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

        _windowCancellations[e.StationId] = _windowCancellations.GetValueOrDefault(e.StationId) + 1;
        station.IncrementCancellations();

        if (ev.HasReservationAtStationId != null)
            _scheduler.CancelEvent((uint)ev.HasReservationAtStationId);
        else
            throw new SkillissueException("Should never cancel without a reservation cancellation token");

        ev.HasReservationAtStationId = null;
    }

    /// <summary>
    /// Called when an EV arrives at a station.
    /// Finds the best compatible charger, joins its queue, and starts charging only if a side is free.
    /// </summary>
    /// <param name="e">The arrival event.</param>
    public void HandleArrivalAtStation(ArriveAtStation e)
    {
        if (_bypassArrivalHandling)
        {
            // TODO: Remove this temporary bypass once the new station-arrival system is in place.
            _eVStore.Free(e.EVId);
            return;
        }

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
                    var plan = state.SessionA.Plan;
                    state.SessionA = null;
                    ev.IsCharging = false;
                    ev.HasReservationAtStationId = null;

                    if (state.SessionB is not null && plan is not null)
                    {
                        var updatedSoC = plan.BSoCWhenAFinish;
                        state.SessionB = state.SessionB with
                        {
                            EV = state.SessionB.EV with { CurrentSoC = updatedSoC }
                        };

                        if (state.SessionB.CancellationToken is { } token)
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
                    var plan = state.SessionB.Plan;
                    state.SessionB = null;
                    ev.IsCharging = false;
                    ev.HasReservationAtStationId = null;

                    if (state.SessionA is not null && plan is not null)
                    {
                        var updatedSoC = plan.ASoCWhenBFinish;
                        state.SessionA = state.SessionA with
                        {
                            EV = state.SessionA.EV with { CurrentSoC = updatedSoC }
                        };

                        if (state.SessionA.CancellationToken is { } token)
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
                    throw new SkillissueException(
                        $"Logic Error: EV {next.EVId} reached Charger {single.Id} but TryConnect failed.");
                }

                state.Queue.Dequeue();

                _metrics.RecordWaitTime(new EVWaitTimeMetric
                {
                    EVId = next.EVId,
                    StationId = state.StationId,
                    ArrivalAtStationTime = next.EV.ArrivalTime,
                    StartChargingTime = simNow,
                });

                state.SessionA = new ActiveSession(next.EVId, next.EV, simNow, null, null, null);
                state.Window = state.Window with { LastEnergyUpdateTime = simNow };

                var result = _integrator.IntegrateSingleToCompletion(
                    simNow, single.MaxPowerKW, single.ChargingPoint, state.SessionA.EV);

                state.SessionA = state.SessionA with { Plan = result };

                if (result?.FinishTimeA is not null)
                {
                    var token = _scheduler.ScheduleEvent(new EndCharging(next.EVId, single.Id, result.FinishTimeA.Value));
                    state.SessionA = state.SessionA with { CancellationToken = token };
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

                    var session = new ActiveSession(candidate.EVId, candidate.EV, simNow, side, null, null);
                    state.Window = state.Window with { LastEnergyUpdateTime = simNow };
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
                    if (wasAloneA && state.SessionA?.CancellationToken is { } tA) _scheduler.CancelEvent(tA);
                    if (wasAloneB && state.SessionB?.CancellationToken is { } tB) _scheduler.CancelEvent(tB);
                }

                if (state.SessionA is null && state.SessionB is null) break;

                var carA = state.SessionA?.EV ?? state.SessionB!.EV with { CurrentSoC = state.SessionB.EV.TargetSoC };
                var carB = state.SessionB?.EV ?? state.SessionA!.EV with { CurrentSoC = state.SessionA.EV.TargetSoC };

                var dualResult = _integrator.IntegrateDualToCompletion(
                    simNow, dual.MaxPowerKW, dual.ChargingPoint, carA, carB);

                if (state.SessionA is not null)
                    state.SessionA = state.SessionA with { Plan = dualResult };

                if (state.SessionB is not null)
                    state.SessionB = state.SessionB with { Plan = dualResult };

                if (state.SessionA is not null && dualResult?.FinishTimeA is not null)
                {
                    var token = _scheduler.ScheduleEvent(new EndCharging(state.SessionA.EVId, dual.Id, dualResult.FinishTimeA.Value));
                    state.SessionA = state.SessionA with { CancellationToken = token };
                }

                if (state.SessionB is not null && dualResult?.FinishTimeB is not null)
                {
                    var token = _scheduler.ScheduleEvent(new EndCharging(state.SessionB.EVId, dual.Id, dualResult.FinishTimeB.Value));
                    state.SessionB = state.SessionB with { CancellationToken = token };
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
            || state.Window.DeliveredKWh > 0;

        if (!hasActivity) return;

        var window = state.Window;
        window.HadActivity = true;
        window.QueueSize = state.Queue.Count;
        state.Window = window;
    }
}