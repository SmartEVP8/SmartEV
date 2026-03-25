namespace Engine.Services;

using System;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Charging.ChargingModel.Chargepoint;
using Core.Shared;
using Core.Vehicles;
using Engine.Vehicles;
using Engine.Events;
using Engine.Routing;
using Engine.Metrics;
using Engine.Utils;

/// <summary>
/// Tracks an active charging session at one side of a charger.
/// </summary>
public record ChargingSession(
    int EVId,
    ConnectedEV EV,
    Time StartTime,
    ChargingSide? Side); // null for single chargers

/// <summary>
/// Tracks the runtime state of a charger, active sessions, waiting queue, and last integration result.
/// </summary>
public class ChargerState(ChargerBase charger)
{
    /// <summary>
    /// Gets charger this state belongs to.
    /// </summary>
    public ChargerBase Charger { get; } = charger;

    /// <summary>
    /// Gets the queue of EVs waiting to charge at this charger, in order of arrival.
    /// </summary>
    public Queue<(int EVId, ConnectedEV EV)> Queue { get; } = new();

    /// <summary>
    /// Gets or sets the active charging session at side A, or null if free. Always used for single chargers.
    /// </summary>
    public ChargingSession? SessionA { get; set; }

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
public class StationService
{
    private readonly Dictionary<int, List<ChargerState>> _stationChargers = [];
    private readonly Dictionary<int, ChargerState> _chargerIndex = [];
    private readonly Dictionary<ushort, Station> _stationIndex = [];
    private readonly ChargingIntegrator _integrator;
    private readonly EventScheduler _scheduler;
    private readonly PathDeviator _pathDeviator;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="StationService"/> class.
    /// </summary>
    /// <param name="stations">The collection of stations to manage.</param>
    /// <param name="integrator">The charging integrator to use for simulating charging sessions.</param>
    /// <param name="scheduler">The event scheduler to use for scheduling future events.</param>
    /// <param name="pathDeviator">The path deviator to use for calculating route deviations through charging stations.</param>
    /// <param name="random">The random instance to use for stochastic sampling throughout the service.</param>
    /// <param name="metricsService">The metrics service to use for recording simulation metrics.</param>
    public StationService(
        ICollection<Station> stations,
        ChargingIntegrator integrator,
        EventScheduler scheduler,
        PathDeviator pathDeviator,
        Random random)
    {
        _integrator = integrator;
        _scheduler = scheduler;
        _pathDeviator = pathDeviator;
        _random = random;

        foreach (var station in stations)
        {
            var states = station.Chargers.Select(c => new ChargerState(c)).ToList();
            _stationChargers[station.Id] = states;
            foreach (var cs in states)
                _chargerIndex[cs.Charger.Id] = cs;
        }
    }

    /// <summary>
    /// Returns the charger state for the given charger id, or null if not found.
    /// </summary>
    /// <param name="chargerId">The id of the charger.</param>
    /// <returns>The charger state for the given charger id, or null if not found.</returns>
    public ChargerState? GetChargerState(int chargerId)
        => _chargerIndex.TryGetValue(chargerId, out var state) ? state : null;

    /// <summary>
    /// Handles a reservation request from an EV to a station.
    /// If the EV already has an active reservation, the existing arrival event is cancelled before proceeding.
    /// Calculates the detoured path through the station, updates the EV's journey, and schedules a new arrival event
    /// with a ±20% on the estimated travel time.
    /// </summary>
    /// <param name="e">The reservation request event.</param>
    /// <param name="evStore">The EV store used to retrieve the requesting EV.</param>
    public void HandleReservationRequest(ReservationRequest e, EVStore evStore)
    {
        var ev = evStore.Get(e.EVId);
        if (!_stationIndex.TryGetValue(e.StationId, out var station))
            return;

        if (ev.HasReservationAtStationId.HasValue)
            _scheduler.CancelEvent(e.EVId);

        station.IncrementReservations();

        var currentTime = _scheduler.CurrentTime;
        var (deviation, newPolyline) = _pathDeviator.CalculateDetourDeviation(
        ev.Journey, currentTime, station.Position);

        var originalRemainingSeconds = ev.Journey.OriginalDuration - ev.Journey.TimeElapsed(currentTime);
        var totalTravelTime = originalRemainingSeconds + deviation;
        var addTravelDuration = 1f + (_random.NextSingle() * 0.4f) - 0.2f;
        var arrivalTime = (Time)(currentTime + (uint)(totalTravelTime * addTravelDuration));

        ev.Journey.Path = Polyline6ToPoints.DecodePolyline(newPolyline);
        ev.Journey.UpdateRunningSumDeviation(deviation);
        ev.HasReservationAtStationId = e.StationId;

        _scheduler.ScheduleEvent(new ArriveAtStation(e.EVId, e.StationId, arrivalTime));
    }

    /// <summary>
    /// Handles a cancellation request from an EV, decrementing the station's active reservation count,
    /// clearing the reservation from the EV, and cancelling the scheduled arrival event.
    /// </summary>
    /// <param name="e">The cancellation request event.</param>
    /// <param name="evStore">The EV store used to retrieve the requesting EV.</param>
    public void HandleCancelRequest(CancelRequest e, EVStore evStore)
    {
        var ev = evStore.Get(e.EVId);
        if (!_stationIndex.TryGetValue(e.StationId, out var station))
            return;

        station.DecrementReservations();

        if (ev.HasReservationAtStationId.HasValue)
            _scheduler.CancelEvent(e.EVId);

        ev.HasReservationAtStationId = null;
    }

    /// <summary>
    /// Called when an EV arrives at a station.
    /// Finds the best compatible charger, joins its queue, and starts charging only if a side is free.
    /// </summary>
    /// <param name="e">The arrival event.</param>
    /// <param name="ev">The connected EV.</param>
    public void HandleArrivalAtStation(ArriveAtStation e, ConnectedEV ev)
    {
        if (!_stationChargers.TryGetValue(e.StationId, out var chargers))
            return;

        var target = chargers
            .Where(cs => cs.Charger.GetSockets().Contains(ev.Socket))
            .OrderBy(cs => cs.IsFree ? 0 : 1)
            .ThenBy(cs => cs.Queue.Count)
            .FirstOrDefault();

        if (target is null)
            return;

        target.Queue.Enqueue((e.EVId, ev));

        if (target.IsFree)
            StartCharging(target, e.Time);
    }

    /// <summary>
    /// Called when a charging session ends for a specific EV.
    /// Uses the internally stored IntegrationResult to update remaining car SoC.
    /// </summary>
    /// <param name="e">The EndCharging event containing the EVId, ChargerId, and Time of the event.</param>
    public void HandleEndCharging(EndCharging e)
    {
        if (!_chargerIndex.TryGetValue(e.ChargerId, out var state))
            return;

        var result = state.LastResult;
        state.LastResult = null;

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
                            EV = state.SessionB.EV with { CurrentSoC = updatedSoC }
                        };
                        _scheduler.CancelEvent(state.SessionB.EVId);

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

                    if (state.SessionA is not null && result is not null)
                    {
                        var updatedSoC = result.ASoCWhenBFinish;
                        state.SessionA = state.SessionA with
                        {
                            EV = state.SessionA.EV with { CurrentSoC = updatedSoC }
                        };
                        _scheduler.CancelEvent(state.SessionA.EVId);

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

    /// <summary>
    /// Connects queued cars to free sides, runs the integrator, stores the result,
    /// and schedules EndCharging events.
    /// </summary>
    /// <param name="state">The charger state.</param>
    /// <param name="simNow">The current simulation time.</param>
    private void StartCharging(ChargerState state, Time simNow)
    {
        switch (state.Charger)
        {
            case SingleCharger single:
                {
                    if (state.SessionA is not null) break;
                    if (!state.Queue.TryDequeue(out var next)) break;

                    if (!single.ChargingPoint.TryConnect(next.EV.Socket))
                    {
                        StartCharging(state, simNow);
                        break;
                    }

                    state.SessionA = new ChargingSession(next.EVId, next.EV, simNow, null);

                    var result = _integrator.IntegrateSingleToCompletion(
                        simNow, single.MaxPowerKW, single.ChargingPoint, state.SessionA.EV);

                    state.LastResult = result;

                    _scheduler.ScheduleEvent(
                        new EndCharging(next.EVId, single.Id, result.FinishTimeA!.Value));
                    break;
                }

            case DualCharger dual:
                {
                    var wasAloneA = state.SessionA is not null && state.SessionB is null;
                    var wasAloneB = state.SessionB is not null && state.SessionA is null;
                    var hadBothBefore = state.SessionA is not null && state.SessionB is not null;
                    var oldFinishA = state.LastResult?.FinishTimeA;
                    var oldFinishB = state.LastResult?.FinishTimeB;

                    // Fill empty sides from queue
                    while (state.Queue.TryPeek(out var candidate))
                    {
                        var side = dual.ChargingPoint.TryConnect(candidate.EV.Socket);
                        if (side is null) break;
                        state.Queue.Dequeue();
                        var session = new ChargingSession(candidate.EVId, candidate.EV, simNow, side);
                        if (side == ChargingSide.Left) state.SessionA = session;
                        else state.SessionB = session;
                    }

                    var nowHasBoth = state.SessionA is not null && state.SessionB is not null;
                    if (!hadBothBefore && nowHasBoth && (wasAloneA || wasAloneB))
                    {
                        if (wasAloneA && oldFinishA is not null)
                            _scheduler.CancelEvent(state.SessionA!.EVId);
                        else if (wasAloneB && oldFinishB is not null)
                            _scheduler.CancelEvent(state.SessionB!.EVId);
                    }

                    if (state.SessionA is null && state.SessionB is null) break;

                    var carA = state.SessionA?.EV
                        ?? state.SessionB!.EV with { CurrentSoC = state.SessionB.EV.TargetSoC };
                    var carB = state.SessionB?.EV
                        ?? state.SessionA!.EV with { CurrentSoC = state.SessionA.EV.TargetSoC };

                    var dualResult = _integrator.IntegrateDualToCompletion(
                        simNow, dual.MaxPowerKW, dual.ChargingPoint, carA, carB);

                    state.LastResult = dualResult;

                    if (state.SessionA is not null)
                        _scheduler.ScheduleEvent(new EndCharging(state.SessionA.EVId, dual.Id, dualResult.FinishTimeA!.Value));

                    if (state.SessionB is not null)
                        _scheduler.ScheduleEvent(new EndCharging(state.SessionB.EVId, dual.Id, dualResult.FinishTimeB!.Value));

                    break;
                }
        }
    }
}