namespace API.Services;

using Core.Charging;
using Engine.Events;
using Engine.Services;
using Engine.Services.StationServiceHelpers;
using API.Protocol;
using Core.Vehicles;

/// <summary>
/// Pre-built EV position data, published from the simulation thread.
/// </summary>
public record EVPositionEntry(int EvId, double Lat, double Lon);

/// <summary>
/// Pre-built simulation aggregate stats, published from the simulation thread.
/// </summary>
public record SimulationSnapshotData(uint TotalEvs, uint TotalCharging, uint SimulationTimeMs);

/// <summary>
/// Handles snapshot requests from the client.
/// Simulation-level snapshots (state + EV positions) are pre-built on the
/// simulation thread via <see cref="PublishStats"/> and <see cref="PublishPositions"/>.
/// Per-station snapshots are built on demand (safe: called from sim thread).
/// </summary>
public class SnapshotHandler(
    StationService stationService,
    IReadOnlyDictionary<int, EV> evStore,
    EventScheduler eventScheduler)
{
    private SimulationSnapshotData? _latestStats;
    private EVPositionEntry[] _latestPositions = [];

    /// <summary>
    /// Publishes the latest simulation stats snapshot. Called from the simulation thread.
    /// </summary>
    /// <param name="data">The snapshot data.</param>
    public void PublishStats(SimulationSnapshotData data) => _latestStats = data;

    /// <summary>
    /// Publishes the latest EV position snapshot. Called from the simulation thread.
    /// </summary>
    /// <param name="data">The snapshot data.</param>
    public void PublishPositions(EVPositionEntry[] data) => _latestPositions = data;

    /// <summary>
    /// Reads the pre-built simulation stats snapshot and converts to protobuf.
    /// </summary>
    /// <returns>The envelope containing the simulation snapshot response.</returns>
    public Envelope BuildSimulationSnapshot()
    {
        var stats = _latestStats;

        return new Envelope
        {
            StateUpdate = new SimulationSnapshot
            {
                TotalEvs = stats?.TotalEvs ?? 0,
                TotalCharging = stats?.TotalCharging ?? 0,
                SimulationTimeMs = stats?.SimulationTimeMs ?? 0,
            },
        };
    }

    /// <summary>
    /// Reads the pre-built EV position snapshot and converts to protobuf.
    /// </summary>
    /// <returns>The envelope containing the EV positions.</returns>
    public Envelope BuildEVPositionSnapshot()
    {
        var result = new GetEVsInViewPort();

        foreach (var ev in _latestPositions)
        {
            result.EvPositions.Add(new EVPosition
            {
                EvId = ev.EvId,
                Pos = new Position { Lat = ev.Lat, Lon = ev.Lon },
            });
        }

        return new Envelope { GetEvsInViewport = result };
    }

    /// <summary>
    /// Builds a station snapshot response by querying the engine for the current state of the specified station.
    /// </summary>
    /// <param name="station">The station for which to build the snapshot.</param>
    /// <returns>The envelope containing the station snapshot response.</returns>
    public Envelope BuildStationSnapshot(Station station)
    {
        var stationState = new StationState { StationId = station.Id };

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
            StartTimeMs = session.StartTime,
            FinishTimeMs = finishTime ?? 0,
        };

    private EVOnRoute[] GetEVsOnRoute(Station station)
    {
        var evsOnRoute = station.Reservations.GetEVsOnRoute;

        var result = new List<EVOnRoute>();
        foreach (var evId in evsOnRoute)
        {
            var ev = evStore[evId];
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
