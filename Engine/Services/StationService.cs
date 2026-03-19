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
/// Tracks the runtime state of a charger — active sessions and waiting queue.
/// </summary>
public class ChargerState(ChargerBase charger)
{
    public ChargerBase Charger { get; } = charger;
    public Queue<(uint EVId, ConnectedCar Car)> Queue { get; } = new();
    public ChargingSession? SessionA { get; set; }
    public ChargingSession? SessionB { get; set; } // dual only

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

    }

    public void HandleCancelRequest(CancelRequest e)
        => _scheduler.CancelEvent(e);

    /// <summary>
    /// Called when an EV arrives at a station.
    /// Finds the best compatible charger, joins its queue, and starts charging if free.
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

        // Prefer a free charger, otherwise join the shortest queue
        var target = compatible.FirstOrDefault(cs => cs.IsFree)
            ?? compatible.MinBy(cs => cs.Queue.Count)!;

        target.Queue.Enqueue((e.EVId, car));
        StartCharging(target, (uint)e.Time);
    }

    /// <summary>
    /// Called when a charging session ends for a specific EV.
    /// Disconnects the car, updates the remaining car's SoC if dual, then restarts.
    /// </summary>
    public void HandleEndCharging(EndCharging e, IntegrationResult result)
    {
        var state = _stationChargers.Values
            .SelectMany(cs => cs)
            .FirstOrDefault(cs => cs.Charger.Id == e.ChargerId);

        if (state is null) return;

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

                    // Update remaining car's SoC and cancel its stale EndCharging event
                    if (state.SessionB is not null)
                    {
                        var updatedCar = state.SessionB.Car with
                        {
                            CurrentSoC = state.SessionB.Car.CurrentSoC
                                + (result.EnergyDeliveredKWhB / state.SessionB.Car.CapacityKWh)
                        };
                        state.SessionB = state.SessionB with { Car = updatedCar };
                        _scheduler.CancelEndCharging(state.SessionB.EVId);
                    }
                }
                else if (state.SessionB?.EVId == e.EVId)
                {
                    dual.ChargingPoint.Disconnect(ChargingSide.Right);
                    state.SessionB = null;

                    if (state.SessionA is not null)
                    {
                        var updatedCar = state.SessionA.Car with
                        {
                            CurrentSoC = state.SessionA.Car.CurrentSoC
                                + (result.EnergyDeliveredKWhA / state.SessionA.Car.CapacityKWh)
                        };
                        state.SessionA = state.SessionA with { Car = updatedCar };
                        _scheduler.CancelEndCharging(state.SessionA.EVId);
                    }
                }

                break;
        }

        // Remaining car recomputes, or next in queue connects
        StartCharging(state, (uint)e.Time);
    }

    /// <summary>
    /// Connects queued cars to free sides, runs the integrator, and schedules EndCharging events.
    /// Single entry point for all session scheduling — called on arrival, after a car finishes,
    /// and when a new car joins a partially occupied dual charger.
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
                        StartCharging(state, simNow); // incompatible socket — try next
                        break;
                    }

                    state.SessionA = new ChargingSession(next.EVId, next.Car, simNow, null);

                    var result = _integrator.IntegrateSingleToCompletion(
                        simNow, single.MaxPowerKW, single.ChargingPoint, state.SessionA.Car);

                    _scheduler.ScheduleEvent(
                        new EndCharging(next.EVId, single.Id, result.FinishTimeA!.Value),
                        result.FinishTimeA!.Value);
                    break;
                }

            case DualCharger dual:
                {
                    // Fill any free sides from the queue
                    while (state.Queue.TryPeek(out var candidate))
                    {
                        var side = dual.ChargingPoint.TryConnect(candidate.Car.Socket);
                        if (side is null) break; // both sides occupied

                        state.Queue.Dequeue();
                        var session = new ChargingSession(candidate.EVId, candidate.Car, simNow, side);
                        if (side == ChargingSide.Left) state.SessionA = session;
                        else state.SessionB = session;
                    }

                    // Run integrator based on how many cars are connected
                    if (state.SessionA is not null && state.SessionB is not null)
                    {
                        var result = _integrator.IntegrateDualToCompletion(
                            simNow, dual.MaxPowerKW, dual.ChargingPoint,
                            state.SessionA.Car, state.SessionB.Car);

                        _scheduler.ScheduleEvent(
                            new EndCharging(state.SessionA.EVId, dual.Id, result.FinishTimeA!.Value),
                            result.FinishTimeA!.Value);
                        _scheduler.ScheduleEvent(
                            new EndCharging(state.SessionB.EVId, dual.Id, result.FinishTimeB!.Value),
                            result.FinishTimeB!.Value);
                    }
                    else if (state.SessionA is not null)
                    {
                        var empty = state.SessionA.Car with
                        {
                            CurrentSoC = state.SessionA.Car.TargetSoC
                        };

                        var result = _integrator.IntegrateDualToCompletion(
                            simNow, dual.MaxPowerKW, dual.ChargingPoint,
                            state.SessionA.Car, empty);

                        _scheduler.ScheduleEvent(
                            new EndCharging(state.SessionA.EVId, dual.Id, result.FinishTimeA!.Value),
                            result.FinishTimeA!.Value);
                    }
                    else if (state.SessionB is not null)
                    {
                        var empty = state.SessionB.Car with
                        {
                            CurrentSoC = state.SessionB.Car.TargetSoC
                        };

                        var result = _integrator.IntegrateDualToCompletion(
                            simNow, dual.MaxPowerKW, dual.ChargingPoint,
                            state.SessionB.Car, empty);

                        _scheduler.ScheduleEvent(
                            new EndCharging(state.SessionB.EVId, dual.Id, result.FinishTimeA!.Value),
                            result.FinishTimeA!.Value);
                    }

                    break;
                }
        }
    }
}