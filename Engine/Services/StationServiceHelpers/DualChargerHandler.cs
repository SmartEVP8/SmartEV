// DualChargerHandler.cs
namespace Engine.Services.StationServiceHelpers;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Shared;
using Engine.Events;
using Engine.Metrics;
using Engine.Metrics.Events;

/// <summary>
/// Handles the session lifecycle for a <see cref="DualCharger"/>,
/// managing connection, power distribution, integration, scheduling,
/// and disconnection for up to two vehicles simultaneously.
/// </summary>
/// <param name="charger">The dual charger this handler manages.</param>
/// <param name="state">The runtime state of the charger.</param>
/// <param name="integrator">The charging integrator used to simulate sessions.</param>
/// <param name="scheduler">The event scheduler used to schedule end charging events.</param>
/// <param name="metrics">The metrics service used to record wait times.</param>
public class DualChargerHandler(
    DualCharger charger,
    ChargerState state,
    ChargingIntegrator integrator,
    EventScheduler scheduler,
    MetricsService metrics)
    : IChargerHandler
{
    /// <summary>
    /// Connects any queued EVs to free sides, cancels stale scheduled events if pairing changed,
    /// re-integrates both sessions with updated power distribution, and schedules new end events.
    /// Does nothing if both sides are occupied and the queue is empty.
    /// </summary>
    /// <param name="simNow">The current simulation time.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the charger has no active sessions after attempting to connect queued vehicles,
    /// indicating a logic error in connection tracking.
    /// </exception>
    public void StartNext(Time simNow)
    {
        var wasAloneA = state.SessionA is not null && state.SessionB is null;
        var wasAloneB = state.SessionB is not null && state.SessionA is null;

        ConnectQueuedVehicles(simNow);

        if (state.SessionA is null && state.SessionB is null && state.Queue.Count > 0)
        {
            throw new InvalidOperationException(
                $"Logic Error: DualCharger {charger.Id} is empty but failed to connect EV {state.Queue.Peek().EVId}.");
        }

        CancelStaleEventsIfPairingChanged((wasAloneA, wasAloneB));

        var result = IntegrateDual(simNow);

        if (state.SessionA is not null)
            state.SessionA = state.SessionA with { Plan = result };
        if (state.SessionB is not null)
            state.SessionB = state.SessionB with { Plan = result };

        ScheduleEndEvents(result);
    }

    /// <summary>
    /// Ends the session for the given EV, disconnects its side, updates the remaining session's
    /// SoC if the other side is still active, and clears the finished session slot.
    /// </summary>
    /// <param name="evId">The id of the EV whose session is ending.</param>
    /// <param name="simNow">The current simulation time.</param>
    /// <returns>
    /// The final SoC from the integration plan, or the EV's last known SoC if no plan exists.
    /// Returns null if neither active session belongs to the given EV.
    /// </returns>
    public double? EndSession(int evId, Time simNow)
    {
        if (state.SessionA?.EVId == evId)
        {
            var finalSoC = state.SessionA.Plan?.SocA ?? state.SessionA.EV.CurrentSoC;
            var bSoC = state.SessionA.Plan?.BSoCWhenAFinish;
            charger.Disconnect(ChargingSide.Left);
            state.SessionA = null;
            state.SessionB = UpdateRemainingSession(ChargingSide.Right, bSoC, state.SessionB);
            return finalSoC;
        }

        if (state.SessionB?.EVId == evId)
        {
            var finalSoC = state.SessionB.Plan?.SocB ?? state.SessionB.EV.CurrentSoC;
            var aSoC = state.SessionB.Plan?.ASoCWhenBFinish;
            charger.Disconnect(ChargingSide.Right);
            state.SessionB = null;
            state.SessionA = UpdateRemainingSession(ChargingSide.Left, aSoC, state.SessionA);
            return finalSoC;
        }

        return null;
    }

    /// <summary>
    /// Dequeues and connects waiting EVs to any free sides until both sides are occupied
    /// or the queue is empty.
    /// </summary>
    /// <param name="simNow">The current simulation time.</param>
    private void ConnectQueuedVehicles(Time simNow)
    {
        while (state.Queue.TryPeek(out var candidate))
        {
            var side = charger.TryConnect();
            if (side is null) break;

            state.Queue.Dequeue();

            metrics.RecordWaitTime(new EVWaitTimeMetric
            {
                EVId = candidate.EVId,
                StationId = state.StationId,
                ArrivalAtStationTime = candidate.EV.ArrivalTime,
                StartChargingTime = simNow,
            });

            var session = new ActiveSession(candidate.EVId, candidate.EV, simNow, side, null, null);
            state.Window = state.Window with { LastEnergyUpdateTime = simNow };

            if (side == ChargingSide.Left) state.SessionA = session;
            else state.SessionB = session;
        }
    }

    /// <summary>
    /// Cancels the stale end charging event for the previously solo session if a second vehicle
    /// has just joined, since the power distribution and finish time will change.
    /// </summary>
    /// <param name="before">Whether side A or B was occupied before the new vehicle connected.</param>
    private void CancelStaleEventsIfPairingChanged((bool hadA, bool hadB) before)
    {
        var nowHasBoth = state.SessionA is not null && state.SessionB is not null;
        if (before is { hadA: true, hadB: true } || !nowHasBoth) return;

        if (before.hadA && !before.hadB && state.SessionA?.CancellationToken is { } tA)
            scheduler.CancelEvent(tA);

        if (before.hadB && !before.hadA && state.SessionB?.CancellationToken is { } tB)
            scheduler.CancelEvent(tB);
    }

    /// <summary>
    /// Runs the dual integration for both active sessions. When only one side is occupied,
    /// a phantom already-finished car is used for the empty side so the integrator can run normally.
    /// </summary>
    /// <param name="simNow">The current simulation time.</param>
    /// <returns>
    /// The integration result for both sides, or null if neither side has an active session.
    /// </returns>
    private IntegrationResult? IntegrateDual(Time simNow)
    {
        if (state.SessionA is null && state.SessionB is null) return null;

        var carA = state.SessionA?.EV
            ?? state.SessionB!.EV with { CurrentSoC = state.SessionB.EV.TargetSoC };
        var carB = state.SessionB?.EV
            ?? state.SessionA!.EV with { CurrentSoC = state.SessionA.EV.TargetSoC };

        return integrator.IntegrateDualToCompletion(simNow, charger.MaxPowerKW, charger, carA, carB);
    }

    /// <summary>
    /// Schedules end charging events for any active sessions that have a known finish time in the integration result.
    /// </summary>
    /// <param name="result">The integration result containing finish times for each side.</param>
    private void ScheduleEndEvents(IntegrationResult? result)
    {
        if (state.SessionA is not null && result?.FinishTimeA is { } finishA)
        {
            var token = scheduler.ScheduleEvent(new EndCharging(state.SessionA.EVId, charger.Id, state.StationId, finishA));
            state.SessionA = state.SessionA with { CancellationToken = token };
        }

        if (state.SessionB is not null && result?.FinishTimeB is { } finishB)
        {
            var token = scheduler.ScheduleEvent(new EndCharging(state.SessionB.EVId, charger.Id, state.StationId, finishB));
            state.SessionB = state.SessionB with { CancellationToken = token };
        }
    }

    /// <summary>
    /// Updates the remaining session after its partner has finished, applying the recorded SoC
    /// at the moment the other car completed. Disconnects and nulls the session if it has also reached its target.
    /// </summary>
    /// <param name="side">The side the remaining session is connected to.</param>
    /// <param name="updatedSoC">The SoC of this session at the moment the other session finished.</param>
    /// <param name="session">The remaining active session to update, or null if there is none.</param>
    /// <returns>
    /// The updated session, or null if the session was null, the SoC was unavailable,
    /// or the session has already reached its target SoC.
    /// </returns>
    private ActiveSession? UpdateRemainingSession(ChargingSide side, double? updatedSoC, ActiveSession? session)
    {
        if (session is null || updatedSoC is not { } soc) return null;

        session = session with { EV = session.EV with { CurrentSoC = soc } };

        if (session.CancellationToken is { } token)
            scheduler.CancelEvent(token);

        if (soc >= session.EV.TargetSoC)
        {
            charger.Disconnect(side);
            return null;
        }

        return session;
    }
}
