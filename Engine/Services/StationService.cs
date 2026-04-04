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
    /// Gets a value indicating whether the charger has at least one free side.
    /// </summary>
    public bool IsFree => Charger switch
    {
        SingleCharger => SessionA is null,
        DualCharger => SessionA is null || SessionB is null,
        _ => false
    };
}

/// <summary>
/// Service responsible for managing the state of stations and chargers, handling events related to reservations, arrivals, and charging sessions.
/// </summary>
public class StationService : IStationService
{
    private readonly Dictionary<int, List<ChargerState>> _stationChargers = [];
    private readonly Dictionary<int, ChargerState> _chargerIndex = [];
    private readonly Dictionary<ushort, Station> _stationIndex = [];
    private readonly ChargingIntegrator _integrator;
    private readonly EventScheduler _scheduler;
    private readonly EVStore _eVStore;
    private readonly MetricsService _metrics;
    private readonly SnapshotEventHandler _snapshotHandler;
    private readonly bool _bypassArrivalHandling;

    /// <summary>
    /// Initializes a new instance of the <see cref="StationService"/> class.
    /// </summary>
    /// <param name="stations">The collection of stations to manage.</param>
    /// <param name="integrator">The charging integrator to use for simulating charging sessions.</param>
    /// <param name="scheduler">The event scheduler to use for scheduling future events.</param>
    /// <param name="evStore">The storage of current EV's.</param>
    /// <param name="applyNewPath">The path deviator to use for calculating route deviations through charging stations.</param>
    /// <param name="metrics">The metrics service to use for recording metrics.</param>
    /// <param name="snapshotHandler">The snapshot event handler to use for handling snapshot events.</param>
    /// <param name="bypassArrivalHandling">If true, arriving EVs are freed immediately instead of entering charger queues.</param>
    public StationService(
        ICollection<Station> stations,
        ChargingIntegrator integrator,
        EventScheduler scheduler,
        EVStore evStore,
        MetricsService metrics,
        SnapshotEventHandler snapshotHandler,
        bool bypassArrivalHandling = false)
    {
        _integrator = integrator;
        _scheduler = scheduler;
        _eVStore = evStore;
        _metrics = metrics;
        _snapshotHandler = snapshotHandler;
        _bypassArrivalHandling = bypassArrivalHandling;

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
    /// Handles a cancellation request from an EV, decrementing the station's active reservation count,
    /// clearing the reservation from the EV, and cancelling the scheduled arrival event.
    /// </summary>
    /// <param name="e">The cancellation request event.</param>
    public void HandleCancelRequest(CancelRequest e)
    {
        ref var ev = ref _eVStore.Get(e.EVId);
        if (!_stationIndex.TryGetValue(e.StationId, out var station))
            return;

        station.IncrementCancellations();

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

        target.Queue.Enqueue((e.EVId, connectedEV));
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

        var result = state.LastResult;
        state.LastResult = null;
        ref var ev = ref _eVStore.Get(e.EVId);
        switch (state.Charger)
        {
            case SingleCharger single:
                single.ChargingPoint.Disconnect();

                state.SessionA = null;
                ev.HasReservationAtStationId = null;
                break;

            case DualCharger dual:
                if (state.SessionA?.EVId == e.EVId)
                {
                    dual.ChargingPoint.Disconnect(ChargingSide.Left);
                    state.SessionA = null;
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
                            state.SessionB = null;
                        }
                    }
                }
                else if (state.SessionB?.EVId == e.EVId)
                {
                    dual.ChargingPoint.Disconnect(ChargingSide.Right);
                    state.SessionB = null;
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
    }
}
