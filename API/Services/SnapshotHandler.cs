namespace API.Services;

using Core.Charging;
using Engine.Events;
using Engine.Services;
using Engine.Services.StationServiceHelpers;
using Engine.Vehicles;
using Protocol;
using Core.Helper;

/// <summary>
/// Handles snapshot requests from the client by querying the engine.
/// </summary>
/// <param name="evStore">The store for managing electric vehicles.</param>
/// <param name="stationService">The service for managing charging stations.</param>
/// <param name="eventScheduler">The event scheduler for getting the current simulation time.</param>
public class SnapshotHandler(
    EVStore evStore,
    StationService stationService,
    EventScheduler eventScheduler)
{
    /// <summary>
    /// Builds a simulation snapshot response by querying the engine for the current state of the simulation.
    /// </summary>
    /// <returns>The envelope containing the simulation snapshot response.</returns>
    public Envelope BuildSimulationSnapshot()
    {
        var snapshot = new SimulationSnapshot
        {
            TotalEvs = evStore.GetTotalEVsInSimulation(),
            TotalCharging = evStore.GetChargingEVCount(),
            SimulationTimeMs = eventScheduler.CurrentTime,
        };

        return new Envelope { StateUpdate = snapshot };
    }

    /// <summary>
    /// Builds a station snapshot response by querying the engine for the current state of the specified station.
    /// </summary>
    /// <param name="stationId">The ID of the station for which to build the snapshot.</param>
    /// <returns>The envelope containing the station snapshot response.</returns>
    public Envelope BuildStationSnapshot(ushort stationId)
    {
        var stationState = new StationState { StationId = stationId };

        var station = stationService.GetStation(stationId);
        if (station == null)
        {
            Log.Warn(0, 0, $"Station with ID {stationId} not found", ("StationId", stationId));
            return new Envelope { StationStateResponse = stationState };
        }

        foreach (var charger in station.Chargers)
        {
            var chargerHandlers = stationService.GetChargerHandler(charger.Id);
            var chargerState = CreateChargerState(chargerHandlers);
            stationState.ChargerStates.Add(chargerState);
        }

        stationState.EvsOnRoute.AddRange(GetEVsOnRoute(station));

        return new Envelope { StationStateResponse = stationState };
    }

    private ChargerState CreateChargerState(IChargerHandler chargerHandler)
    {
        var (sessionA, sessionB) = chargerHandler.GetSessions();
        var charger = chargerHandler.Charger;
        var schedule = chargerHandler.EstimateWaitTime(eventScheduler.CurrentTime).Schedule;


        var chargerState = new ChargerState
        {
            IsActive = !charger.IsFree,
            Utilization = GetUtilization(charger),
            ChargerId = (uint)charger.Id,
            QueueSize = (uint)charger.Queue.Count,
        };

        if (sessionA is not null)
            chargerState.EvsCharging.Add(CreateEVChargerState(sessionA, sessionA.Plan?.CarA.FinishTime));

        if (sessionB is not null && sessionB.Plan is not null && sessionB.Plan.CarB is not null)
            chargerState.EvsCharging.Add(CreateEVChargerState(sessionB, sessionB.Plan.CarB.FinishTime));

        foreach (var pair in charger.Queue.Zip(schedule, (ev, time) => new { ev, time }))
        {
            chargerState.EvsInQueue.Add(new EVChargerState
            {
                EvId = pair.ev.EVId,
                Soc = (float)pair.ev.CurrentSoC,
                TargetSoc = (float)pair.ev.TargetSoC,
                FinishTimeMs = pair.time.FinishTime,
            });
        }

        return chargerState;
    }

    private static float GetUtilization(ChargerBase charger)
    {
        return charger switch
        {
            SingleCharger s when s.Session?.EV is { } ev =>
                CalculateUtilization(s.GetPowerOutput(s.MaxPowerKW, ev.CurrentSoC), ev.MaxChargeRateKW, s.MaxPowerKW),
            DualCharger d =>
                CalculateDualUtilization(d),
            _ => 0f
        };
    }

    private static float CalculateUtilization(double power, double maxRate, double limit) => (float)(Math.Min(power, maxRate) / limit);

    private static float CalculateDualUtilization(DualCharger d)
    {
        var evA = d.SessionA?.EV;
        var evB = d.SessionB?.EV;

        if (evA is null && evB is null) return 0f;

        var (pA, pB) = d.GetPowerDistribution(
            d.MaxPowerKW,
            evA?.CurrentSoC ?? 0,
            evB?.CurrentSoC ?? 0,
            evA?.MaxChargeRateKW ?? 0,
            evB?.MaxChargeRateKW ?? 0);

        var delivered = Math.Min(pA, evA?.MaxChargeRateKW ?? 0) +
                        Math.Min(pB, evB?.MaxChargeRateKW ?? 0);

        return (float)(delivered / d.MaxPowerKW);
    }

    private static EVChargerState CreateEVChargerState(ActiveSession session, uint? finishTime) =>
        new()
        {
            EvId = session.EVId,
            Soc = (float)session.EV.CurrentSoC,
            TargetSoc = (float)session.EV.TargetSoC,
            FinishTimeMs = finishTime ?? 0,
        };

    private EVOnRoute[] GetEVsOnRoute(Station station)
    {
        var evsOnRoute = station.Reservations.GetEVsOnRoute;

        var result = new List<EVOnRoute>();
        foreach (var evId in evsOnRoute)
        {
            var ev = evStore.Get(evId);
            var evOnRoute = new EVOnRoute { EvId = evId };

            foreach (var waypoint in ev.Journey.Current.Waypoints)
            {
                evOnRoute.Waypoints.Add(new Position
                {
                    Lat = waypoint.Latitude,
                    Lon = waypoint.Longitude,
                });
            }

            result.Add(evOnRoute);
        }

        return [.. result];
    }
}
