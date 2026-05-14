namespace Engine.Services.StationServiceHelpers;

using Core.Charging;
using Core.Charging.ChargingModel;
using Core.Shared;
using Core.Vehicles;
using Serilog;
using Engine.Events;
using Engine.Metrics;
using Engine.Utils;
using Engine.Metrics.Events;

/// <summary>
/// Manages all charger state and charging logic for a single station.
/// </summary>
public class StationHandler
{
    private readonly Dictionary<int, (ChargerBase State, IChargerHandler Handler)> _chargerIndex = [];
    private readonly Dictionary<int, uint> _arrivalTimes = [];
    private readonly Station _station;
    private readonly IReadOnlyDictionary<int, EV> _evs;
    private readonly EventScheduler _scheduler;
    private readonly MetricsService _metrics;

    /// <summary>
    /// Precomputed greedy assignment of all reservations to chargers, in arrival order.
    /// </summary>
    private List<PlanEntry>? _plan;

    /// <summary>
    /// The minimum charger availability before any reservations are accounted for.
    /// Valid whenever <see cref="_plan"/> is non-null.
    /// </summary>
    private Time _planInitialAvailability;
    private Time _costPlanInitialAvailability;
    private List<PlanEntry>? _costPlan;

    private readonly record struct PlanEntry(Time ArrivalTime, Time MinChargerAvailability);

    /// <summary>
    /// Gets the station model associated with this handler.
    /// </summary>
    public Station Station => _station;

    /// <summary>
    /// Initializes a new instance of the <see cref="StationHandler"/> class.
    /// </summary>
    /// <param name="station">The station entity containing chargers and reservations.</param>
    /// <param name="integrator">The charging integrator for power calculations.</param>
    /// <param name="scheduler">The event scheduler for simulation events.</param>
    /// <param name="evs">The store containing all EV state data.</param>
    /// <param name="metrics">The service for recording simulation metrics.</param>
    public StationHandler(
        Station station,
        ChargingIntegrator integrator,
        EventScheduler scheduler,
        IReadOnlyDictionary<int, EV> evs,
        MetricsService metrics)
    {
        _station = station;
        _evs = evs;
        _scheduler = scheduler;
        _metrics = metrics;
        foreach (var charger in station.Chargers)
        {
            IChargerHandler handler = charger switch
            {
                SingleCharger s => new SingleChargerHandler(s, integrator, scheduler, evs),
                DualCharger d => new DualChargerHandler(d, integrator, scheduler, evs),
                _ => throw
                    new InvalidOperationException($"Unknown charger type: {charger.GetType()}")
            };
            _chargerIndex[charger.Id] = (charger, handler);
        }
    }

    /// <summary>
    /// Gets the collection of IDs for all chargers managed by this station.
    /// </summary>
    public IEnumerable<int> ChargerIds => _chargerIndex.Keys;

    /// <summary>
    /// Retrieves the logic handler for a specific charger.
    /// </summary>
    /// <param name="chargerId">The unique identifier of the charger.</param>
    /// <returns>The handler implementing <see cref="IChargerHandler"/>.</returns>
    /// <exception cref="SkillissueException">Thrown if the charger ID is not found.</exception>
    public IChargerHandler GetChargerHandler(int chargerId)
        => _chargerIndex.TryGetValue(chargerId, out var pair)
            ? pair.Handler
            : throw new SkillissueException($"Trying to get charger handler {chargerId} which does not exist.");

    /// <summary>
    /// Invalidates the charging logic plan only. The cost plan is time-based and unaffected.
    /// </summary>
    public void InvalidatePlan() => _plan = null;

    /// <summary>
    /// Finds the best compatible charger, joins its queue, and starts charging only if a side is free.
    /// </summary>
    /// <param name="e">The arrival event details.</param>
    public void HandleArrivalAtStation(ArriveAtStation e)
    {
        InvalidatePlan();

        var ev = e.EV;
        ev.Advance(e.Time);

        if (ev.Battery.StateOfCharge >= e.TargetSoC)
        {
            Log.Error("EV {@EVId} wants to charge to a SoC: {TargetSoC}, which is lower than its current SoC: {CurrentSoC}.", ev.Id, e.TargetSoC, ev.Battery.StateOfCharge);
            throw new SkillissueException($"EV wants to charge to a SoC: {e.TargetSoC}, which is lower than its current SoC: {ev.Battery.StateOfCharge}.");
        }

        var target = _station.Chargers
            .OrderBy(cs => cs.IsFree ? 0 : 1)
            .ThenBy(cs => cs.Queue.Count)
            .FirstOrDefault()
            ?? throw new SkillissueException($"Logic Error: Station {e.Station.Id} has no chargers.");

        var connectedEV = new ConnectedEV(
            ev.Id,
            CurrentSoC: ev.Battery.StateOfCharge,
            TargetSoC: e.TargetSoC,
            CapacityKWh: ev.Battery.MaxCapacityKWh,
            MaxChargeRateKW: ev.Battery.MaxChargeRateKW,
            ArrivalTime: e.Time);

        _arrivalTimes[ev.Id] = e.Time;
        target.Queue.Enqueue(connectedEV);
        ev.EVState = EVState.Queueing;
        target.UpdateWindowStats();

        if (target.IsFree)
            StartChargingNextCar(target, e.Time);
    }

    /// <summary>
    /// Ends the charging session, updates EV state, and starts the next car.
    /// Cross-station reservation cleanup is left to the caller (StationService).
    /// </summary>
    /// <param name="e">The end charging event details.</param>
    public void HandleEndCharging(EndCharging e)
    {
        InvalidatePlan();

        if (!_chargerIndex.TryGetValue(e.Charger.Id, out var entry))
            return;

        var (charger, handler) = entry;
        charger.AccumulateEnergy(e.Time);

        var ev = e.EV;
        var finalSoC = handler.EndSession(ev.Id, e.Time);

        if (finalSoC is { } soc)
            ev.Battery.StateOfCharge = (float)Math.Clamp(soc, 0d, 1d);

        if (!_arrivalTimes.TryGetValue(ev.Id, out var arrivalTime))
        {
            Log.Error("Logic Error: Missing arrival time for EV {@EVId} at EndCharging.", ev.Id);
            throw new SkillissueException($"Logic Error: Missing arrival time for EV {ev.Id} at EndCharging.");
        }

        _arrivalTimes.Remove(ev.Id);
        var timeAtStation = e.Time - arrivalTime;
        ev.EVState = EVState.Driving;

        if (ev.CanCompleteJourney(timeAtStation, ev.Preferences.MinAcceptableCharge))
        {
            Log.Information("Scheduling arrival at destination for EV {@EVId} with final SoC {FinalSoC}.", ev.Id, ev.Battery.StateOfCharge);
            _scheduler.ScheduleEvent(new ArriveAtDestination(ev, e.Time));
        }
        else
        {
            Log.Information("EV {@EVId} has completed its charging but cannot continue to its destination with SoC {FinalSoC}. Scheduling search for candidate stations.", ev.Id, ev.Battery.StateOfCharge);
            _scheduler.ScheduleEvent(new FindCandidateStations(ev, ev.TimeAtNextFindCandidateCheck(e.Time)));
        }

        StartChargingNextCar(charger, e.Time);
    }

    /// <summary>
    /// Returns the earliest absolute simulation time at which a charger will be free for an EV
    /// arriving at <paramref name="arrival"/>, using a time-based cached plan so that frequent
    /// arrivals/departures do not cause constant recomputation during cost evaluation.
    /// </summary>
    /// <param name="simNow">Current simulation time.</param>
    /// <param name="arrival">The projected arrival time of the EV.</param>
    /// <returns>Absolute time when a charger side becomes available.</returns>
    public Time ExpectedWaitTime(Time simNow, Time arrival)
    {
        EnsurePlan(simNow);
        var index = _costPlan?.FindLastIndex(p => p.ArrivalTime <= arrival) ?? -1;
        return index >= 0
            ? _costPlan![index].MinChargerAvailability
            : _costPlanInitialAvailability;
    }

    /// <summary>
    /// Builds the full integrated charger availability plan over all reservations if it is not
    /// already cached. Each entry in <see cref="_plan"/> answers: "after greedily assigning
    /// every reservation up to and including this one, what is the earliest a new EV could start
    /// charging?".
    /// </summary>
    /// <param name="simNow">Current simulation time.</param>
    private void EnsurePlan(Time simNow)
    {
        _costPlan = [];

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

        _planInitialAvailability = waitTimes.TryPeek(out _, out var initMin) ? initMin : simNow;

        foreach (var reservation in _station.Reservations.AllReservations)
        {
            waitTimes.TryDequeue(out var chargerId, out var currentAvailableAt);

            var battery = _evs[reservation.EVId].Battery;
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
            waitTimes.TryPeek(out _, out var minAfter);
            _costPlan.Add(new PlanEntry(reservation.TimeOfArrival, minAfter));
        }
    }

    /// <summary>
    /// Transitions a charger from idle/queueing to active charging for the next EV in line.
    /// </summary>
    /// <param name="charger">The charger state object.</param>
    /// <param name="simNow">Current simulation time.</param>
    private void StartChargingNextCar(ChargerBase charger, Time simNow)
    {
        if (!charger.Queue.TryPeek(out var top))
            return;

        charger.AccumulateEnergy(simNow);
        _chargerIndex[charger.Id].Handler.StartNext(simNow, _station);

        _evs[top.EVId].EVState = EVState.Charging;

        _metrics.RecordWaitTime(new WaitTimeInQueueMetric
        {
            EVId = top.EVId,
            StationId = _station.Id,
            ArrivalAtStationTime = top.ArrivalTime,
            StartChargingTime = simNow,
        });
        charger.UpdateWindowStats();
    }
}
