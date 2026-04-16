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
/// <param name="integrator">The charging integrator used to simulate sessions.</param>
/// <param name="scheduler">The event scheduler used to schedule end charging events.</param>
/// <param name="metrics">The metrics service used to record wait times.</param>
public class DualChargerHandler(
    DualCharger charger,
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
    /// <param name="stationId">The ID of the station.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the charger has no active sessions after attempting to connect queued vehicles,
    /// indicating a logic error in connection tracking.
    /// </exception>
    public void StartNext(Time simNow, ushort stationId)
    {
        var wasAloneA = charger.SessionA is not null && charger.SessionB is null;
        var wasAloneB = charger.SessionB is not null && charger.SessionA is null;

        ConnectQueuedVehicles(simNow, stationId);

        if (charger.SessionA is null && charger.SessionB is null && charger.Queue.Count > 0)
        {
            throw new InvalidOperationException(
                $"Logic Error: DualCharger {charger.Id} is empty but failed to connect EV {charger.Queue.Peek().EVId}.");
        }

        CancelStaleEventsIfPairingChanged((wasAloneA, wasAloneB));

        var result = IntegrateDual(simNow);

        if (charger.SessionA is not null)
            charger.SessionA = charger.SessionA with { Plan = result };
        if (charger.SessionB is not null)
            charger.SessionB = charger.SessionB with { Plan = result };

        ScheduleEndEvents(result, stationId);
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
        if (charger.SessionA?.EVId == evId)
        {
            var finalSoC = charger.SessionA.Plan?.CarA.Soc ?? charger.SessionA.EV.CurrentSoC;
            var bSoC = charger.SessionA.Plan?.CarA.PartnerSoCAtFinish;
            charger.Disconnect(ChargingSide.Left);
            charger.SessionA = null;
            charger.SessionB = UpdateRemainingSession(ChargingSide.Right, bSoC, charger.SessionB);
            return finalSoC;
        }

        if (charger.SessionB?.EVId == evId)
        {
            var finalSoC = charger.SessionB.Plan?.CarB?.Soc ?? charger.SessionB.EV.CurrentSoC;
            var aSoC = charger.SessionB.Plan?.CarB?.PartnerSoCAtFinish;
            charger.Disconnect(ChargingSide.Right);
            charger.SessionB = null;
            charger.SessionA = UpdateRemainingSession(ChargingSide.Left, aSoC, charger.SessionA);
            return finalSoC;
        }

        return null;
    }

    /// <summary>
    /// Dequeues and connects waiting EVs to any free sides until both sides are occupied
    /// or the queue is empty.
    /// </summary>
    /// <param name="simNow">The current simulation time.</param>
    /// <param name="stationId">The ID of the station.</param>
    private void ConnectQueuedVehicles(Time simNow, ushort stationId)
    {
        while (charger.Queue.TryPeek(out var candidate))
        {
            var side = charger.TryConnect();
            if (side is null) break;

            charger.Queue.Dequeue();

            metrics.RecordWaitTime(new EVWaitTimeMetric
            {
                EVId = candidate.EVId,
                StationId = stationId,
                ArrivalAtStationTime = candidate.EV.ArrivalTime,
                StartChargingTime = simNow,
            });

            var session = new ActiveSession(candidate.EVId, candidate.EV, simNow, side, null, null);
            charger.Window = charger.Window with { LastEnergyUpdateTime = simNow };

            if (side == ChargingSide.Left) charger.SessionA = session;
            else charger.SessionB = session;
        }
    }

    /// <summary>
    /// Cancels the stale end charging event for the previously solo session if a second vehicle
    /// has just joined, since the power distribution and finish time will change.
    /// </summary>
    /// <param name="before">Whether side A or B was occupied before the new vehicle connected.</param>
    private void CancelStaleEventsIfPairingChanged((bool hadA, bool hadB) before)
    {
        var nowHasBoth = charger.SessionA is not null && charger.SessionB is not null;
        if (before is { hadA: true, hadB: true } || !nowHasBoth) return;

        if (before.hadA && !before.hadB && charger.SessionA?.CancellationToken is { } tA)
            scheduler.CancelEvent(tA);

        if (before.hadB && !before.hadA && charger.SessionB?.CancellationToken is { } tB)
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
        if (charger.SessionA is null && charger.SessionB is null) return null;

        var carA = charger.SessionA?.EV
            ?? charger.SessionB!.EV with { CurrentSoC = charger.SessionB.EV.TargetSoC };
        var carB = charger.SessionB?.EV
            ?? charger.SessionA!.EV with { CurrentSoC = charger.SessionA.EV.TargetSoC };

        return integrator.IntegrateDualToCompletion(simNow, charger.MaxPowerKW, charger, carA, carB);
    }

    /// <summary>
    /// Schedules end charging events for any active sessions that have a known finish time in the integration result.
    /// </summary>
    /// <param name="result">The integration result containing finish times for each side.</param>
    /// <param name="stationId">The ID of the station.</param>
    private void ScheduleEndEvents(IntegrationResult? result, ushort stationId)
    {
        if (charger.SessionA is not null && result?.CarA.FinishTime is { } finishA)
        {
            var token = scheduler.ScheduleEvent(new EndCharging(charger.SessionA.EVId, charger.Id, stationId, finishA));
            charger.SessionA = charger.SessionA with { CancellationToken = token };
        }

        if (charger.SessionB is not null && result?.CarB?.FinishTime is { } finishB)
        {
            var token = scheduler.ScheduleEvent(new EndCharging(charger.SessionB.EVId, charger.Id, stationId, finishB));
            charger.SessionB = charger.SessionB with { CancellationToken = token };
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
