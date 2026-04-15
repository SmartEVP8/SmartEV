namespace API.Services;

using Core.Charging;
using Engine.Events;
using Engine.Services;
using Engine.Vehicles;
using Protocol;

/// <summary>
/// Handles snapshot requests from the client by querying the engine.
/// </summary>
/// <param name="evStore">The store for managing electric vehicles.</param>
/// <param name="stationService">The service for managing charging stations.</param>
/// <param name="eventScheduler">The event scheduler for getting the current simulation time.</param>
/// <param name="logger">The logger for recording events and errors.</param>
public class SnapshotHandler(
    EVStore evStore,
    StationService stationService,
    EventScheduler eventScheduler,
    ILogger<SnapshotHandler> logger)
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
            logger.LogWarning("Station with ID {StationId} not found", stationId);
            return new Envelope { StationStateResponse = stationState };
        }

        foreach (var charger in station.Chargers)
        {
            var chargerState = CreateChargerState(charger);
            stationState.ChargerStates.Add(chargerState);
        }

        stationState.EvsOnRoute.AddRange(GetEVsOnRoute(station));

        return new Envelope { StationStateResponse = stationState };
    }

    private ChargerState CreateChargerState(ChargerBase charger)
    {
        ActiveSession? sessionA;
        ActiveSession? sessionB;
        switch (charger)
        {
            case SingleCharger s:
                sessionA = s.Session;
                sessionB = null;
                break;

            case DualCharger d:
                sessionA = d.SessionA;
                sessionB = d.SessionB;
                break;
            default:
                throw new InvalidOperationException($"Unknown charger type: {charger.GetType()}");
        }

        var chargerState = new ChargerState
        {
            IsActive = !charger.IsFree,
            Utilization = GetUtilization(charger),
            ChargerId = (uint)charger.Id,
            QueueSize = (uint)charger.Queue.Count,
        };

        if (sessionA is not null)
            chargerState.EvsCharging.Add(CreateEVChargerState(sessionA));

        if (sessionB is not null)
            chargerState.EvsCharging.Add(CreateEVChargerState(sessionB));

        foreach (var (evId, ev) in charger.Queue)
        {
            chargerState.EvsInQueue.Add(new EVChargerState
            {
                EvId = evId,
                Soc = (float)ev.CurrentSoC,
                TargetSoc = (float)ev.TargetSoC,
            });
        }

        return chargerState;
    }

    private static float GetUtilization(ChargerBase charger)
    {
        return charger switch
        {
            SingleCharger s when s.Session is not null && s.Session.EV is { } ev =>
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

    private static EVChargerState CreateEVChargerState(ActiveSession session) =>
        new()
        {
            EvId = session.EVId,
            Soc = (float)session.EV.CurrentSoC,
            TargetSoc = (float)session.EV.TargetSoC,
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
