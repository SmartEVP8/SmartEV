namespace Engine.Services.StationServiceHelpers;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Shared;
using Engine.Events;
using Engine.Metrics;
using Engine.Metrics.Events;
using Engine.Utils;

/// <summary>
/// Handles the session lifecycle for a <see cref="SingleCharger"/>,
/// managing connection, integration, scheduling, and disconnection for one vehicle at a time.
/// </summary>
/// <param name="charger">The single charger this handler manages.</param>
/// <param name="state">The runtime state of the charger.</param>
/// <param name="integrator">The charging integrator used to simulate the session.</param>
/// <param name="scheduler">The event scheduler used to schedule end charging events.</param>
/// <param name="metrics">The metrics service used to record wait times.</param>
public class SingleChargerHandler(
    SingleCharger charger,
    ChargerState state,
    ChargingIntegrator integrator,
    EventScheduler scheduler,
    MetricsService metrics)
    : IChargerHandler
{
    /// <summary>
    /// Dequeues the next EV and starts a charging session if the charger is free.
    /// Does nothing if a session is already active or the queue is empty.
    /// </summary>
    /// <param name="simNow">The current simulation time.</param>
    /// <exception cref="SkillissueException">
    /// Thrown if the connector reports as occupied when it should be free,
    /// indicating a logic error in session tracking.
    /// </exception>
    public void StartNext(Time simNow)
    {
        if (state.SessionA is not null) return;
        if (!state.Queue.TryPeek(out var next)) return;

        if (!charger.TryConnect())
        {
            throw new SkillissueException(
                $"Logic Error: EV {next.EVId} reached Charger {charger.Id} but TryConnect failed.");
        }

        state.Queue.Dequeue();

        metrics.RecordWaitTime(new EVWaitTimeMetric
        {
            EVId = next.EVId,
            StationId = state.StationId,
            ArrivalAtStationTime = next.EV.ArrivalTime,
            StartChargingTime = simNow,
        });

        state.SessionA = new ActiveSession(next.EVId, next.EV, simNow, null, null, null);
        state.Window = state.Window with { LastEnergyUpdateTime = simNow };

        var result = integrator.IntegrateSingleToCompletion(simNow, charger.MaxPowerKW, charger, next.EV);
        state.SessionA = state.SessionA with { Plan = result };

        if (result.FinishTimeA is { } finishTime)
        {
            var token = scheduler.ScheduleEvent(new EndCharging(next.EVId, charger.Id, state.StationId, finishTime));
            state.SessionA = state.SessionA with { CancellationToken = token };
        }
    }

    /// <summary>
    /// Ends the active session for the given EV, disconnects the connector, and clears the session slot.
    /// </summary>
    /// <param name="evId">The id of the EV whose session is ending.</param>
    /// <param name="simNow">The current simulation time.</param>
    /// <returns>
    /// The final SoC from the integration plan, or the EV's last known SoC if no plan exists.
    /// Returns null if the active session does not belong to the given EV.
    /// </returns>
    public double? EndSession(int evId, Time simNow)
    {
        if (state.SessionA?.EVId != evId) return null;

        var finalSoC = state.SessionA.Plan?.SocA ?? state.SessionA.EV.CurrentSoC;
        charger.Disconnect();
        state.SessionA = null;
        return finalSoC;
    }
}
