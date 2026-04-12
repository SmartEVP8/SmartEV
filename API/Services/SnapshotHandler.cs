namespace API.Services;

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
            SimulationTimeMs = (ulong)eventScheduler.CurrentTime,
        };

        return new Envelope { StateUpdate = snapshot };
    }

    /// <summary>
    /// Builds a station snapshot response by querying the engine for the current state of the specified station.
    /// </summary>
    /// <param name="request">The request for the station snapshot.</param>
    /// <returns>The envelope containing the station snapshot response.</returns>
    public Envelope BuildStationSnapshot(GetStationSnapshot request)
    {
        var stationId = request.StationId;
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
        var random = new Random(chargerId);
        var tempUtilization = random.NextSingle();

        var engineChargerState = stationService.GetChargerState(chargerId);
        if (engineChargerState is null)
            return null!;

        var chargerState = new Protocol.ChargerState
        {
            IsActive = !engineChargerState.IsFree,
            Utilization = tempUtilization,
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