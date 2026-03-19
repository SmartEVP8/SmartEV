namespace Engine.Services;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Charging.ChargingModel.Chargepoint;
using Engine.Events;

/// <summary>
/// Tracks an active charging session at one side of a charger.
/// </summary>
public record ChargingSession(
    uint EVId,
    ConnectedCar Car,
    uint StartTime,
    ChargingSide? Side); // null for single chargers

/// <summary>
/// Tracks the runtime state of a charger — active sessions, waiting queue, and last integration result.
/// </summary>
public class ChargerState(ChargerBase charger)
{
    public ChargerBase Charger { get; } = charger;
    public Queue<(uint EVId, ConnectedCar Car)> Queue { get; } = new();
    public ChargingSession? SessionA { get; set; }
    public ChargingSession? SessionB { get; set; } // dual only
    public IntegrationResult? LastResult { get; set; } // stored when StartCharging runs

    public bool IsFree => charger switch
    {
        SingleCharger => SessionA is null,
        DualCharger => SessionA is null || SessionB is null,
        _ => false
    };
}

public class StationService
{
    private readonly Dictionary<ushort, List<ChargerState>> _stationChargers = [];
    private readonly ChargingIntegrator _integrator;
    private readonly EventScheduler _scheduler;

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

    public void HandleReservationRequest(ReservationRequest e)
    {
        _scheduler.ScheduleEvent(new ArriveAtStation(e.EVId, e.StationId, e.Time), e.Time + 50);
    }

    public void HandleCancelRequest(CancelRequest e)
        => _scheduler.CancelEvent(e);

    /// <summary>
    /// Called when an EV arrives at a station.
    /// Finds the best compatible charger, joins its queue, and starts charging only if a side is free.
    /// </summary>
    public void HandleArrivalAtStation(ArriveAtStation e, ConnectedCar car)
    {
        if (!_stationChargers.TryGetValue(e.StationId, out var chargers))
            return;

        var compatible = chargers
            .Where(cs => cs.Charger.GetSockets().Contains(car.Socket))
            .ToList();

        if (compatible.Count == 0)
            return;

        var target = compatible.FirstOrDefault(cs => cs.IsFree)
            ?? compatible.MinBy(cs => cs.Queue.Count)!;

        target.Queue.Enqueue((e.EVId, car));

        if (target.IsFree)
            StartCharging(target, e.Time);
    }

    /// <summary>
    /// Called when a charging session ends for a specific EV.
    /// Uses the internally stored IntegrationResult to update remaining car SoC.
    /// </summary>
    public void HandleEndCharging(EndCharging e)

    {
        var state = _stationChargers.Values
            .SelectMany(cs => cs)
            .FirstOrDefault(cs => cs.Charger.Id == e.ChargerId);
        Console.WriteLine($"HandleEndCharging EVId={e.EVId} ChargerId={e.ChargerId} stateFound={state is not null}");

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
                        var updatedSoC = Math.Min(
                            state.SessionB.Car.CurrentSoC + (result.EnergyDeliveredKWhB / state.SessionB.Car.CapacityKWh),
                            state.SessionB.Car.TargetSoC);

                        state.SessionB = state.SessionB with
                        {
                            Car = state.SessionB.Car with { CurrentSoC = updatedSoC }
                        };
                        _scheduler.CancelEndCharging(state.SessionB.EVId, state.LastResult!.FinishTimeB!.Value);

                        if (updatedSoC >= state.SessionB.Car.TargetSoC)
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
                        var updatedSoC = Math.Min(
                            state.SessionA.Car.CurrentSoC + (result.EnergyDeliveredKWhA / state.SessionA.Car.CapacityKWh),
                            state.SessionA.Car.TargetSoC);

                        state.SessionA = state.SessionA with
                        {
                            Car = state.SessionA.Car with { CurrentSoC = updatedSoC }
                        };
                        _scheduler.CancelEndCharging(state.SessionA.EVId, state.LastResult!.FinishTimeA!.Value);

                        if (updatedSoC >= state.SessionA.Car.TargetSoC)
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

                    if (!single.ChargingPoint.TryConnect(next.Car.Socket))
                    {
                        StartCharging(state, simNow);
                        break;
                    }

                    state.SessionA = new ChargingSession(next.EVId, next.Car, simNow, null);

                    var result = _integrator.IntegrateSingleToCompletion(
                        simNow, single.MaxPowerKW, single.ChargingPoint, state.SessionA.Car);

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

                    Console.WriteLine($"StartCharging DUAL simNow={simNow} hadBothBefore={hadBothBefore} wasAloneA={wasAloneA} wasAloneB={wasAloneB} QueueCount={state.Queue.Count}");

                    // Fill empty sides from queue
                    while (state.Queue.TryPeek(out var candidate))
                    {
                        var side = dual.ChargingPoint.TryConnect(candidate.Car.Socket);
                        Console.WriteLine($"Sessions after connect: A.EVId={state.SessionA?.EVId} B.EVId={state.SessionB?.EVId} side returned={side}");

                        if (side is null) break;
                        state.Queue.Dequeue();
                        var session = new ChargingSession(candidate.EVId, candidate.Car, simNow, side);
                        if (side == ChargingSide.Left) state.SessionA = session;
                        else state.SessionB = session;
                    }
                    Console.WriteLine($"After connecting from queue: SessionA={(state.SessionA is not null ? state.SessionA.EVId.ToString() : "null")} SessionB={(state.SessionB is not null ? state.SessionB.EVId.ToString() : "null")}");

                    var nowHasBoth = state.SessionA is not null && state.SessionB is not null;
                    var needsReschedule = !hadBothBefore && nowHasBoth && (wasAloneA || wasAloneB);

                    if (needsReschedule)
                    {
                        if (wasAloneA && oldFinishA is not null)
                            _scheduler.CancelEndCharging(state.SessionA!.EVId, oldFinishA.Value); // stale event IS at this time
                        else if (wasAloneB && oldFinishB is not null)
                            _scheduler.CancelEndCharging(state.SessionB!.EVId, oldFinishB.Value);
                    }
                    Console.WriteLine($"After potential cancel: QueueCount={state.Queue.Count} NextEvent={_scheduler.PeekNextEvent()?.GetType().Name}");

                    // Schedule charging based on current state
                    if (state.SessionA is not null && state.SessionB is not null)
                    {
                        var result = _integrator.IntegrateDualToCompletion(
                            simNow, dual.MaxPowerKW, dual.ChargingPoint,
                            state.SessionA.Car, state.SessionB.Car);

                        state.LastResult = result;

                        Console.WriteLine($"DUAL simNow={simNow} SocA={state.SessionA.Car.CurrentSoC}/{state.SessionA.Car.TargetSoC} SocB={state.SessionB.Car.CurrentSoC}/{state.SessionB.Car.TargetSoC} FinishA={result.FinishTimeA} FinishB={result.FinishTimeB}");

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
                            simNow, dual.MaxPowerKW, dual.ChargingPoint,
                            state.SessionA.Car,
                            state.SessionA.Car with { CurrentSoC = state.SessionA.Car.TargetSoC });

                        state.LastResult = result;

                        _scheduler.ScheduleEvent(
                            new EndCharging(state.SessionA.EVId, dual.Id, result.FinishTimeA!.Value),
                            result.FinishTimeA!.Value);
                    }
                    else if (state.SessionB is not null)
                    {
                        var result = _integrator.IntegrateDualToCompletion(
                            simNow, dual.MaxPowerKW, dual.ChargingPoint,
                            state.SessionB.Car with { CurrentSoC = state.SessionB.Car.TargetSoC },
                            state.SessionB.Car);

                        state.LastResult = result;

                        Console.WriteLine($"simNow={simNow} FinishTimeB={result.FinishTimeB} SocB_current={state.SessionB.Car.CurrentSoC} SocB_target={state.SessionB.Car.TargetSoC}");

                        _scheduler.ScheduleEvent(
                            new EndCharging(state.SessionB.EVId, dual.Id, result.FinishTimeB!.Value),
                            result.FinishTimeB!.Value);
                    }

                    break;
                }
        }
    }
}