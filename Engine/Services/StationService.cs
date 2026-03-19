namespace Engine.Services;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Charging.ChargingModel.Chargepoint;
using Core.Vehicles;
using Engine.Events;

/// <summary>
/// Tracks an active charging session at one side of a charger.
/// </summary>
public record ChargingSession(
    int EVId,
    ConnectedEV ev,
    uint StartTime,
    ChargingSide? Side); // null for single chargers

/// <summary>
/// Tracks the runtime state of a charger, active sessions, waiting queue, and last integration result.
/// </summary>
/// <param name="Charger">The charger this state belongs to.</param>
/// <param name="Queue">EVs waiting to charge at this charger, in order of arrival.</param>
/// <param name="SessionA">Active session at side A, or null if free.</param>
/// <param name="SessionB">Active session at side B, or null if free. Always null for single chargers.</param>
/// <param name="LastResult">The result of the last.
public class ChargerState(ChargerBase charger)
{
    /// <summary>
    /// Gets charger this state belongs to.
    /// </summary>
    public ChargerBase Charger { get; } = charger;

    /// <summary>
    /// Gets the queue of EVs waiting to charge at this charger, in order of arrival.
    /// </summary>
    public Queue<(int EVId, ConnectedEV ev)> Queue { get; } = new();

    /// <summary>
    /// Gets or sets the active charging session at side A, or null if free.
    /// </summary>
    public ChargingSession? SessionA { get; set; }

    /// <summary>
    /// Gets or sets the active charging session at side B, or null if free. Always null for single chargers.
    /// </summary>
    public ChargingSession? SessionB { get; set; }

    /// <summary>
    /// Gets or sets the result of the last integration run for the charger.
    /// </summary>
    public IntegrationResult? LastResult { get; set; } // stored when StartCharging runs

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
public class StationService
{
    private readonly Dictionary<ushort, List<ChargerState>> _stationChargers = [];
    private readonly ChargingIntegrator _integrator;
    private readonly EventScheduler _scheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="StationService"/> class with a list of stations, a charging integrator, and an event scheduler.
    /// </summary>
    /// <param name="stations">The stations to manage.</param>
    /// <param name="integrator">The charging integrator.</param>
    /// <param name="scheduler">The event scheduler.</param>
    public StationService(
        IEnumerable<Station> stations,
        ChargingIntegrator integrator,
        EventScheduler scheduler)
    {
        _integrator = integrator;
        _scheduler = scheduler;

        foreach (var station in stations)
            _stationChargers[station.Id] = [.. station.Chargers.Select(c => new ChargerState(c))];
    }

    // TODO: Handle reservationrequest
    public void HandleReservationRequest(ReservationRequest e)
        => _scheduler.ScheduleEvent(new ArriveAtStation(e.EVId, e.StationId, e.Time), e.Time);

    // TODO: handle cancelrequest
    public void HandleCancelRequest(CancelRequest e)
        => _scheduler.CancelEvent(e);

    /// <summary>
    /// Called when an EV arrives at a station.
    /// Finds the best compatible charger, joins its queue, and starts charging only if a side is free.
    /// </summary>
    /// <param name="e">The arrival event containing EV and station information.</param>
    /// <param name="ev">The connected EV that has arrived.</param>
    public void HandleArrivalAtStation(ArriveAtStation e, ConnectedEV ev)
    {
        if (!_stationChargers.TryGetValue(e.StationId, out var chargers))
            return;

        var compatible = chargers
            .Where(cs => cs.Charger.GetSockets().Contains(ev.Socket))
            .ToList();

        if (compatible.Count == 0)
            return;

        var target = compatible.FirstOrDefault(cs => cs.IsFree)
            ?? compatible.MinBy(cs => cs.Queue.Count)!;

        target.Queue.Enqueue((e.EVId, ev));

        if (target.IsFree)
            StartCharging(target, e.Time);
    }

    /// <summary>
    /// Called when a charging session ends for a specific EV.
    /// Uses the internally stored IntegrationResult to update remaining car SoC.
    /// </summary>
    /// <param name="e">The EndCharging event containing EV and charger information.</param>
    public void HandleEndCharging(EndCharging e)
    {
        var state = _stationChargers.Values
            .SelectMany(cs => cs)
            .FirstOrDefault(cs => cs.Charger.Id == e.ChargerId);

        if (state is null) return;

        var result = state.LastResult;

        switch (state.Charger)
        {
            case SingleCharger single:
                single.ChargingPoint.Disconnect();
                state.SessionA = null;
                break;

            case DualCharger dual:
                if (state.SessionA?.EVId == e.EVId)
                {
                    dual.ChargingPoint.Disconnect(ChargingSide.Left);
                    state.SessionA = null;

                    if (state.SessionB is not null && result is not null)
                    {
                        var updatedSoC = result.BSoCWhenAFinish;
                        state.SessionB = state.SessionB with
                        {
                            ev = state.SessionB.ev with { CurrentSoC = updatedSoC }
                        };
                        _scheduler.CancelEndCharging(state.SessionB.EVId, state.LastResult!.FinishTimeB!.Value);

                        if (updatedSoC >= state.SessionB.ev.TargetSoC)
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

                    if (state.SessionA is not null && result is not null)
                    {
                        var updatedSoC = result.ASoCWhenBFinish;

                        state.SessionA = state.SessionA with
                        {
                            ev = state.SessionA.ev with { CurrentSoC = updatedSoC }
                        };
                        _scheduler.CancelEndCharging(state.SessionA.EVId, state.LastResult!.FinishTimeA!.Value);

                        if (updatedSoC >= state.SessionA.ev.TargetSoC)
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

    /// <summary>
    /// Connects queued cars to free sides, runs the integrator, stores the result,
    /// and schedules EndCharging events.
    /// </summary>
    private void StartCharging(ChargerState state, uint simNow)
    {
        switch (state.Charger)
        {
            case SingleCharger single:
                {
                    if (state.SessionA is not null) break;
                    if (!state.Queue.TryDequeue(out var next)) break;

                    if (!single.ChargingPoint.TryConnect(next.ev.Socket))
                    {
                        StartCharging(state, simNow);
                        break;
                    }

                    state.SessionA = new ChargingSession(next.EVId, next.ev, simNow, null);

                    var result = _integrator.IntegrateSingleToCompletion(
                        simNow, single.MaxPowerKW, single.ChargingPoint, state.SessionA.ev);

                    state.LastResult = result;

                    _scheduler.ScheduleEvent(
                        new EndCharging(next.EVId, single.Id, result.FinishTimeA!.Value),
                        result.FinishTimeA!.Value);
                    break;
                }

            case DualCharger dual:
                {
                    var wasAloneA = state.SessionA is not null && state.SessionB is null;
                    var wasAloneB = state.SessionB is not null && state.SessionA is null;
                    var hadBothBefore = state.SessionA is not null && state.SessionB is not null;

                    // Capture OLD finish times before LastResult is overwritten
                    var oldFinishA = state.LastResult?.FinishTimeA;
                    var oldFinishB = state.LastResult?.FinishTimeB;

                    // Fill empty sides from queue
                    while (state.Queue.TryPeek(out var candidate))
                    {
                        var side = dual.ChargingPoint.TryConnect(candidate.ev.Socket);
                        if (side is null) break;
                        state.Queue.Dequeue();
                        var session = new ChargingSession(candidate.EVId, candidate.ev, simNow, side);
                        if (side == ChargingSide.Left) state.SessionA = session;
                        else state.SessionB = session;
                    }

                    var nowHasBoth = state.SessionA is not null && state.SessionB is not null;
                    var needsReschedule = !hadBothBefore && nowHasBoth && (wasAloneA || wasAloneB);

                    if (needsReschedule)
                    {
                        if (wasAloneA && oldFinishA is not null)
                            _scheduler.CancelEndCharging(state.SessionA!.EVId, oldFinishA.Value);
                        else if (wasAloneB && oldFinishB is not null)
                            _scheduler.CancelEndCharging(state.SessionB!.EVId, oldFinishB.Value);
                    }

                    // Schedule charging based on current state
                    if (state.SessionA is not null && state.SessionB is not null)
                    {
                        var result = _integrator.IntegrateDualToCompletion(
                            simNow, dual.MaxPowerKW, dual.ChargingPoint, state.SessionA.ev, state.SessionB.ev);

                        state.LastResult = result;

                        _scheduler.ScheduleEvent(
                            new EndCharging(state.SessionA.EVId, dual.Id, result.FinishTimeA!.Value),
                            result.FinishTimeA!.Value);
                        _scheduler.ScheduleEvent(
                            new EndCharging(state.SessionB.EVId, dual.Id, result.FinishTimeB!.Value),
                            result.FinishTimeB!.Value);
                    }
                    else if (state.SessionA is not null)
                    {
                        var result = _integrator.IntegrateDualToCompletion(
                            simNow,
                            dual.MaxPowerKW,
                            dual.ChargingPoint,
                            state.SessionA.ev,
                            state.SessionA.ev with { CurrentSoC = state.SessionA.ev.TargetSoC });

                        state.LastResult = result;

                        _scheduler.ScheduleEvent(
                            new EndCharging(state.SessionA.EVId, dual.Id, result.FinishTimeA!.Value),
                            result.FinishTimeA!.Value);
                    }
                    else if (state.SessionB is not null)
                    {
                        var result = _integrator.IntegrateDualToCompletion(
                            simNow,
                            dual.MaxPowerKW,
                            dual.ChargingPoint,
                            state.SessionB.ev with { CurrentSoC = state.SessionB.ev.TargetSoC },
                            state.SessionB.ev);

                        state.LastResult = result;

                        _scheduler.ScheduleEvent(
                            new EndCharging(state.SessionB.EVId, dual.Id, result.FinishTimeB!.Value),
                            result.FinishTimeB!.Value);
                    }

                    break;
                }
        }
    }
}