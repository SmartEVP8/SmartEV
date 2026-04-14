namespace API.Services;

using Core.Charging;
using Engine.Events;
using Engine.Services;
using Engine.Services.StationServiceHelpers;
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
            SimulationTimeMs = (ulong)eventScheduler.CurrentTime,
        };

        return new Envelope { StateUpdate = snapshot };
    }

    /// <summary>
    /// Builds a station snapshot response by querying the engine for the current state of the specified station.
    /// </summary>
    /// <param name="stationId">The ID of the station for which to build the snapshot.</param>
    /// <returns>The envelope containing the station snapshot response.</returns>
    public Envelope BuildStationSnapshot(uint stationId)
    {
        var stationState = new StationState { StationId = stationId };

        var station = stationService.GetStation((ushort)stationId);
        if (station == null)
        {
            logger.LogWarning("Station with ID {StationId} not found", stationId);
            return new Envelope { StationStateResponse = stationState };
        }

        foreach (var charger in station.Chargers)
        {
            var chargerState = CreateChargerState(charger.Id);
            stationState.ChargerStates.Add(chargerState);
        }

        stationState.EvsOnRoute.AddRange(GetEVsOnRoute((ushort)stationId));

        return new Envelope { StationStateResponse = stationState };
    }

    private Protocol.ChargerState CreateChargerState(int chargerId)
    {
        var engineChargerState = stationService.GetChargerState(chargerId);
        if (engineChargerState is null)
            return null!;

        var chargerState = new Protocol.ChargerState
        {
            IsActive = !engineChargerState.IsFree,
            Utilization = GetUtilization(engineChargerState),
            ChargerId = (uint)chargerId,
            QueueSize = (uint)engineChargerState.Queue.Count,
        };

        if (engineChargerState.SessionA is not null)
            chargerState.EvsInQueue.Add(CreateEVChargerState(engineChargerState.SessionA));

        if (engineChargerState.SessionB is not null)
            chargerState.EvsInQueue.Add(CreateEVChargerState(engineChargerState.SessionB));

        foreach (var (evId, ev) in engineChargerState.Queue)
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

    private static float GetUtilization(Engine.Services.StationServiceHelpers.ChargerState chargerState)
    {
        switch (chargerState.Charger)
        {
            case SingleCharger s:
                var powerOutput = s.GetPowerOutput(
                    s.MaxPowerKW,
                    chargerState.SessionA?.EV.CurrentSoC ?? 0.0);
                return (float)(powerOutput / s.MaxPowerKW);

            case DualCharger d:
                var (powerA, powerB) = d.GetPowerDistribution(
                    d.MaxPowerKW,
                    chargerState.SessionA?.EV.CurrentSoC ?? 0.0,
                    chargerState.SessionB?.EV.CurrentSoC ?? 0.0,
                    chargerState.SessionA?.EV.MaxChargeRateKW ?? 0.0,
                    chargerState.SessionB?.EV.MaxChargeRateKW ?? 0.0);
                return (float)((powerA + powerB) / d.MaxPowerKW);

            default:
                throw new InvalidOperationException("Unknown charger type");
        }
    }

    private static EVChargerState CreateEVChargerState(ActiveSession session) =>
        new()
        {
            EvId = session.EVId,
            Soc = (float)session.EV.CurrentSoC,
            TargetSoc = (float)session.EV.TargetSoC,
        };

    private EVOnRoute[] GetEVsOnRoute(ushort stationId)
    {
        var evsOnRoute = stationService.GetEVsOnRouteToStation(stationId);

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