namespace Engine.Services.StationServiceHelpers;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Shared;
using Core.Vehicles;
using Core.Helper;
using Engine.Events;
using Engine.Metrics;
using Engine.Utils;
using Engine.Vehicles;

/// <summary>
/// Manages all charger state and charging logic for a single station.
/// </summary>
public class StationHandler
{
    private readonly Dictionary<int, (ChargerBase State, IChargerHandler Handler)> _chargerIndex = [];
    private readonly Dictionary<int, uint> _arrivalTimes = [];
    private readonly Station _station;
    private readonly EVStore _evStore;
    private readonly EventScheduler _scheduler;

    public Station Station => _station;

    public StationHandler(
        Station station,
        ChargingIntegrator integrator,
        EventScheduler scheduler,
        EVStore evStore,
        MetricsService metrics)
    {
        _station = station;
        _evStore = evStore;
        _scheduler = scheduler;

        foreach (var charger in station.Chargers)
        {
            IChargerHandler handler = charger switch
            {
                SingleCharger s => new SingleChargerHandler(s, integrator, scheduler, metrics),
                DualCharger d => new DualChargerHandler(d, integrator, scheduler, metrics),
                _ => throw Log.Error(0, 0,
                    new InvalidOperationException($"Unknown charger type: {charger.GetType()}"),
                    ((string Key, object Value))("Charger", charger))
            };
            _chargerIndex[charger.Id] = (charger, handler);
        }
    }

    public IEnumerable<int> ChargerIds => _chargerIndex.Keys;

    public IChargerHandler GetChargerHandler(int chargerId)
        => _chargerIndex.TryGetValue(chargerId, out var pair)
            ? pair.Handler
            : throw new SkillissueException($"Trying to get charger handler {chargerId} which does not exist.");

    /// <summary>
    /// Finds the best compatible charger, joins its queue, and starts charging only if a side is free.
    /// </summary>
    public void HandleArrivalAtStation(ArriveAtStation e)
    {
        ref var evRef = ref _evStore.Get(e.EVId);
        evRef.Advance(e.Time);
        Log.Verbose(e.EVId, e.Time, $"Handling ArrivalAtStation for EV {e.EVId} at time {e.Time}. Current EV data: {evRef}. SoC: {evRef.Battery.StateOfCharge}. Wants to charge to {e.TargetSoC}");

        if (evRef.Battery.StateOfCharge >= e.TargetSoC)
            throw Log.Error(e.EVId, e.Time,
                new SkillissueException($"EV wants to charge to a SoC: {e.TargetSoC}, which is lower than its current SoC: {evRef.Battery.StateOfCharge}."),
                ((string Key, object Value))("EV", evRef),
                ((string Key, object Value))("TargetSoC", e.TargetSoC));

        var target = _station.Chargers
            .OrderBy(cs => cs.IsFree ? 0 : 1)
            .ThenBy(cs => cs.Queue.Count)
            .FirstOrDefault()
            ?? throw Log.Error(e.EVId, e.Time,
                new SkillissueException($"Logic Error: Station {e.StationId} has no chargers."),
                ((string Key, object Value))("StationId", e.StationId));

        var connectedEV = new ConnectedEV(
            EVId: e.EVId,
            CurrentSoC: evRef.Battery.StateOfCharge,
            TargetSoC: e.TargetSoC,
            CapacityKWh: evRef.Battery.MaxCapacityKWh,
            MaxChargeRateKW: evRef.Battery.MaxChargeRateKW,
            ArrivalTime: e.Time);

        _arrivalTimes[e.EVId] = e.Time;
        target.Queue.Enqueue(connectedEV);
        _evStore.Get(e.EVId).EVState = EVState.Queueing;
        target.UpdateWindowStats();

        if (target.IsFree)
            StartChargingNextCar(target, e.Time);
    }

    /// <summary>
    /// Ends the charging session, updates EV state, and starts the next car.
    /// Cross-station reservation cleanup is left to the caller (StationService).
    /// </summary>
    public void HandleEndCharging(EndCharging e)
    {
        if (!_chargerIndex.TryGetValue(e.ChargerId, out var entry))
            return;

        var (charger, handler) = entry;
        charger.AccumulateEnergy(e.Time);

        var finalSoC = handler.EndSession(e.EVId, e.Time);

        ref var ev = ref _evStore.Get(e.EVId);

        if (finalSoC is { } soc)
            ev.Battery.StateOfCharge = (float)Math.Clamp(soc, 0d, 1d);

        if (!_arrivalTimes.TryGetValue(e.EVId, out var arrivalTime))
            throw Log.Error(e.EVId, e.Time,
                new SkillissueException($"Logic Error: Missing arrival time for EV {e.EVId} at EndCharging."));

        _arrivalTimes.Remove(e.EVId);
        var timeAtStation = e.Time - arrivalTime;
        ev.EVState = EVState.Driving;

        if (ev.CanCompleteJourney(timeAtStation, ev.Preferences.MinAcceptableCharge))
        {
            Log.Info(e.EVId, e.Time, $"EV {e.EVId} has completed its charging and can continue to its destination with SoC {ev.Battery.StateOfCharge}. Scheduling arrival at destination.");
            _scheduler.ScheduleEvent(new ArriveAtDestination(e.EVId, e.Time));
        }
        else
        {
            Log.Info(e.EVId, e.Time, $"EV {e.EVId} has completed its charging but cannot continue to its destination with SoC {ev.Battery.StateOfCharge}. Scheduling search for candidate stations.");
            _scheduler.ScheduleEvent(new FindCandidateStations(e.EVId, ev.TimeAtNextFindCandidateCheck(e.Time)));
        }

        StartChargingNextCar(charger, e.Time);
    }

    public Time ExpectedWaitTime(Time simNow, Time arrival)
    {
        var waitTimes = new PriorityQueue<int, Time>();
        var reservationQueues = new Dictionary<int, List<ConnectedEV>>();
        var initialAvailability = new Dictionary<int, Time>();

        foreach (var charger in _station.Chargers)
        {
            var handler = _chargerIndex[charger.Id].Handler;
            var (availableAt, _) = handler.EstimateWaitTime(simNow);
            var estimatedWait = availableAt + simNow;

            waitTimes.Enqueue(charger.Id, estimatedWait);
            reservationQueues[charger.Id] = [];
            initialAvailability[charger.Id] = estimatedWait;
        }

        var reservationsBefore = _station.Reservations.ReservationsBefore(arrival);

        foreach (var reservation in reservationsBefore)
        {
            waitTimes.TryDequeue(out var chargerId, out var currentAvailableAt);

            var battery = _evStore.Get(reservation.EVId).Battery;

            var connectedEV = new ConnectedEV(
                EVId: reservation.EVId,
                CurrentSoC: reservation.SoCAtArrival,
                TargetSoC: reservation.TargetSoC,
                CapacityKWh: battery.MaxCapacityKWh,
                MaxChargeRateKW: battery.MaxChargeRateKW,
                ArrivalTime: currentAvailableAt);

            var queue = reservationQueues[chargerId];
            queue.Add(connectedEV);

            var handler = _chargerIndex[chargerId].Handler;
            var baseTime = initialAvailability[chargerId];

            var (availableAt, _) = handler.EstimateWaitTime(baseTime, queue);
            waitTimes.Enqueue(chargerId, availableAt + baseTime);
        }

        return waitTimes.TryDequeue(out var _, out var minWait) ? minWait : simNow;
    }

    private void StartChargingNextCar(ChargerBase charger, Time simNow)
    {
        if (!charger.Queue.TryPeek(out var top))
            return;

        Log.Verbose(top.EVId, simNow, $"Starting next charge on charger {charger.Id} at station {_station.Id} at time {simNow}. Next EV in queue: {top.EVId}");
        charger.AccumulateEnergy(simNow);
        _chargerIndex[charger.Id].Handler.StartNext(simNow, _station.Id);
        _evStore.Get(top.EVId).EVState = EVState.Charging;
        charger.UpdateWindowStats();
    }
}
