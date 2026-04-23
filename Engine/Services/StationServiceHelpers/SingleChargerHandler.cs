namespace Engine.Services.StationServiceHelpers;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Shared;
using Engine.Events;
using Engine.Metrics;
using Engine.Metrics.Events;
using Engine.Utils;
using Core.Helper;

/// <summary>
/// Handles the session lifecycle for a <see cref="SingleCharger"/>,
/// managing connection, integration, scheduling, and disconnection for one vehicle at a time.
/// </summary>
/// <param name="charger">The single charger this handler manages.</param>
/// <param name="integrator">The charging integrator used to simulate the session.</param>
/// <param name="scheduler">The event scheduler used to schedule end charging events.</param>
/// <param name="metrics">The metrics service used to record wait times.</param>
public class SingleChargerHandler(
    SingleCharger charger,
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
    /// <param name="stationId">The stations id.</param>
    /// <exception cref="SkillissueException">
    /// Thrown if the connector reports as occupied when it should be free,
    /// indicating a logic error in session tracking.
    /// </exception>
    public void StartNext(Time simNow, ushort stationId)
    {
        if (charger.Session is not null) return;
        if (!charger.Queue.TryPeek(out var next)) return;

        if (!charger.TryConnect())
        {
            throw Log.Error(0, simNow, new SkillissueException(
                $"Logic Error: EV {next.EVId} reached Charger {charger.Id} but TryConnect failed."),
                ((string Key, object Value))("StationId", stationId),
                ((string Key, object Value))("Charger", charger),
                ((string Key, object Value))("NextEV", next));
        }

        charger.Queue.Dequeue();

        metrics.RecordWaitTime(new WaitTimeInQueueMetric
        {
            EVId = next.EVId,
            StationId = stationId,
            ArrivalAtStationTime = next.ArrivalTime,
            StartChargingTime = simNow,
        });

        charger.Session = new ActiveSession(next.EVId, next, simNow, null, null, null);
        charger.Window = charger.Window with { LastEnergyUpdateTime = simNow };

        var result = integrator.IntegrateSingleToCompletion(simNow, charger.MaxPowerKW, charger, next);
        charger.Session = charger.Session with { Plan = result };

        if (result.CarA.FinishTime is { } finishTime)
        {
            Log.Info(charger.Session.EVId, finishTime, $"Scheduling EndCharging event for EV {charger.Session.EVId} on charger {charger.Id} at station {stationId} with finish time {finishTime}.");
            var token = scheduler.ScheduleEvent(new EndCharging(next.EVId, charger.Id, stationId, finishTime));
            charger.Session = charger.Session with { CancellationToken = token };
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
        if (charger.Session?.EVId != evId) return null;

        var finalSoC = charger.Session.Plan?.CarA.Soc ?? charger.Session.EV.CurrentSoC;
        charger.Disconnect();
        charger.Session = null;
        return finalSoC;
    }

    /// <inheritdoc/>
    public (Time AvailableAt, IReadOnlyList<(int EVId, Time FinishTime)> Schedule) EstimateWaitTime(Time simNow, IReadOnlyList<ConnectedEV>? evsOverride = null)
    {
        var availableAt = simNow;
        var evs = evsOverride ?? charger.CreateConnectedEVs(simNow);
        var schedule = new List<(int, Time)>();

        foreach (var ev in evs)
        {
            var result = integrator.IntegrateSingleToCompletion(
                availableAt,
                charger.MaxPowerKW,
                charger,
                ev);

            availableAt = result.CarA.FinishTime ?? throw new InvalidOperationException($"EV {ev.EVId} did not produce a finish time.");
            schedule.Add((ev.EVId, availableAt));
        }

        var availableAtTimestamp = availableAt > simNow ? availableAt - simNow : new Time(0);
        return (availableAtTimestamp, schedule);
    }
}
