namespace Engine.Services;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Shared;
using Engine.Events;
using Engine.Metrics;
using Engine.Utils;
using Engine.Vehicles;
using Engine.Services.StationServiceHelpers;
using Core.Vehicles;
using Core.Helper;

/// <summary>
/// Service responsible for managing the state of stations and chargers, handling events related to reservations, arrivals, and charging sessions.
/// </summary>
public class StationService : IStationService
{
    private readonly Dictionary<int, (ChargerBase State, IChargerHandler Handler)> _chargerIndex = [];
    private readonly Dictionary<ushort, Station> _stationIndex = [];
    private readonly Dictionary<int, uint> _arrivalTimes = [];
    private readonly Dictionary<int, ushort> _evReservations = [];
    private readonly EventScheduler _scheduler;
    private readonly EVStore _eVStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="StationService"/> class.
    /// </summary>
    /// <param name="stations">The collection of stations to manage.</param>
    /// <param name="integrator">The charging integrator to use for simulating charging sessions.</param>
    /// <param name="scheduler">The event scheduler to use for scheduling future events.</param>
    /// <param name="evStore">The storage of current EVs.</param>
    /// <param name="metrics">The metrics service to use for recording metrics.</param>
    public StationService(
        ICollection<Station> stations,
        ChargingIntegrator integrator,
        EventScheduler scheduler,
        EVStore evStore,
        MetricsService metrics)
    {
        _scheduler = scheduler;
        _eVStore = evStore;

        foreach (var station in stations)
        {
            _stationIndex[station.Id] = station;

            foreach (var charger in station.Chargers)
            {
                IChargerHandler handler = charger switch
                {
                    SingleCharger s => new SingleChargerHandler(s, integrator, scheduler, metrics),
                    DualCharger d => new DualChargerHandler(d, integrator, scheduler, metrics),
                    _ => throw Log.Error(0, 0, new InvalidOperationException($"Unknown charger type: {charger.GetType()}"), ((string Key, object Value))("Charger", charger))
                };
                _chargerIndex[charger.Id] = (charger, handler);
            }
        }
    }

    /// <inheritdoc/>
    public Station GetStation(ushort stationId)
        => _stationIndex.TryGetValue(stationId, out var station)
            ? station
            : throw Log.Error(0, 0, new SkillissueException($"Trying to get station {stationId} which does not exist."), ((string Key, object Value))("StationId", stationId));

    /// <summary>Gets the stationId that an EV has a reservation for if any.</summary>
    /// <param name="evId">The id used for checking for a reservation.</param>
    /// <returns>The stationId or null.</returns>
    public ushort? GetReservationStationId(int evId)
        => _evReservations.TryGetValue(evId, out var stationId) ? stationId : null;

    /// <summary>
    /// Creates an reservation on the station and cancels previous reservation if it exists.
    /// </summary>
    /// <param name="reservation">The reservation event.</param>
    /// <param name="stationId">The station that recieves the reservation.</param>
    public void HandleReservation(Reservation reservation, ushort stationId)
    {
        CancelReservation(reservation.EVId);
        _evReservations[reservation.EVId] = stationId;
        GetStation(stationId).Reservations.Reserve(
                new Reservation(reservation.EVId, reservation.TimeOfArrival, reservation.SoCAtArrival, reservation.TargetSoC));
    }

    private void CancelReservation(int evId)
    {
        if (_evReservations.TryGetValue(evId, out var oldStationId))
        {
            GetStation(oldStationId).Reservations.Cancel(evId);
        }
    }

    /// <summary>
    /// Called when an EV arrives at a station.
    /// Finds the best compatible charger, joins its queue, and starts charging only if a side is free.
    /// </summary>
    /// <param name="e">The arrival event.</param>
    public void HandleArrivalAtStation(ArriveAtStation e)
    {
        ref var evRef = ref _eVStore.Get(e.EVId);
        evRef.Advance(e.Time);
        Log.Verbose(e.EVId, e.Time, $"Handling ArrivalAtStation for EV {e.EVId} at time {e.Time}. Current EV data: {evRef}. SoC: {evRef.Battery.StateOfCharge}. Wants to charge to {e.TargetSoC}");
        var chargers = GetStation(e.StationId).Chargers;

        if (evRef.Battery.StateOfCharge >= e.TargetSoC)
            throw Log.Error(e.EVId, e.Time, new SkillissueException($"EV wants to charge to a SoC: {e.TargetSoC}, which is lower than its current SoC: {evRef.Battery.StateOfCharge}."), ((string Key, object Value))("EV", evRef), ((string Key, object Value))("TargetSoC", e.TargetSoC));

        var target = chargers
            .OrderBy(cs => cs.IsFree ? 0 : 1)
            .ThenBy(cs => cs.Queue.Count)
            .FirstOrDefault()
            ?? throw Log.Error(e.EVId, e.Time, new SkillissueException($"Logic Error: Station {e.StationId} has no chargers."), ((string Key, object Value))("StationId", e.StationId));

        var connectedEV = new ConnectedEV(
            EVId: e.EVId,
            CurrentSoC: evRef.Battery.StateOfCharge,
            TargetSoC: e.TargetSoC,
            CapacityKWh: evRef.Battery.MaxCapacityKWh,
            MaxChargeRateKW: evRef.Battery.MaxChargeRateKW,
            ArrivalTime: e.Time);

        _arrivalTimes[e.EVId] = e.Time;
        target.Queue.Enqueue(connectedEV);
        _eVStore.Get(e.EVId).EVState = EVState.Queueing;
        target.UpdateWindowStats();

        if (target.IsFree)
            StartChargingNextCar(target, e.Time, e.StationId);
    }

    /// <summary>
    /// Called when a charging session ends for a specific EV.
    /// Uses the internally stored IntegrationResult to update remaining car SoC.
    /// </summary>
    /// <param name="e">The EndCharging event containing the EVId, ChargerId, and Time of the event.</param>
    public void HandleEndCharging(EndCharging e)
    {
        if (!_chargerIndex.TryGetValue(e.ChargerId, out var entry))
            return;

        var (charger, handler) = entry;
        charger.AccumulateEnergy(e.Time);

        var finalSoC = handler.EndSession(e.EVId, e.Time);

        ref var ev = ref _eVStore.Get(e.EVId);

        if (finalSoC is { } soc)
            ev.Battery.StateOfCharge = (float)Math.Clamp(soc, 0d, 1d);

        if (!_arrivalTimes.TryGetValue(e.EVId, out var arrivalTime))
            throw Log.Error(e.EVId, e.Time, new SkillissueException($"Logic Error: Missing arrival time for EV {e.EVId} at EndCharging."));

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
            _scheduler.ScheduleEvent(new FindCandidateStations(e.EVId, ev.TimeToNextFindCandidateCheck(e.Time)));
        }

        if (!_evReservations.TryGetValue(e.EVId, out var stationId))
            throw Log.Error(e.EVId, e.Time, new SkillissueException("Should have a reservation at this point"));

        CancelReservation(e.EVId);
        StartChargingNextCar(charger, e.Time, stationId);
    }

    private void StartChargingNextCar(ChargerBase charger, Time simNow, ushort stationId)
    {
        if (!charger.Queue.TryPeek(out var top))
        {
            return;
        }

        Log.Verbose(top.EVId, simNow, $"Starting next charge on charger {charger.Id} at station {stationId} at time {simNow}. Next EV in queue: {top.EVId}");
        charger.AccumulateEnergy(simNow);
        _chargerIndex[charger.Id].Handler.StartNext(simNow, stationId);
        _eVStore.Get(top.EVId).EVState = EVState.Charging;
        charger.UpdateWindowStats();
    }
}
