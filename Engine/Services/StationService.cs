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
/// Coordinates station handlers and manages cross-station state (reservations, charger routing).
/// </summary>
public class StationService : IStationService
{
    private readonly Dictionary<ushort, StationHandler> _stationHandlers = [];
    private readonly Dictionary<int, ushort> _chargerToStation = [];
    private readonly Dictionary<int, ushort> _evReservations = [];

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
        foreach (var station in stations)
        {
            var handler = new StationHandler(station, integrator, scheduler, evStore, metrics);
            _stationHandlers[station.Id] = handler;

            foreach (var chargerId in handler.ChargerIds)
                _chargerToStation[chargerId] = station.Id;
        }
    }

    /// <inheritdoc/>
    public Station GetStation(ushort stationId)
        => _stationHandlers.TryGetValue(stationId, out var handler)
            ? handler.Station
            : throw Log.Error(0, 0, new SkillissueException($"Trying to get station {stationId} which does not exist."), ((string Key, object Value))("StationId", stationId));

    public IChargerHandler GetChargerHandler(int chargerId)
        => _chargerToStation.TryGetValue(chargerId, out var stationId)
            ? _stationHandlers[stationId].GetChargerHandler(chargerId)
            : throw new SkillissueException($"Trying to get charger handler {chargerId} which does not exist.");

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
        GetStation(stationId).Reservations.Reserve(reservation);
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
        => GetStationHandler(e.StationId).HandleArrivalAtStation(e);

    /// <summary>
    /// Called when a charging session ends for a specific EV.
    /// Uses the internally stored IntegrationResult to update remaining car SoC.
    /// </summary>
    /// <param name="e">The EndCharging event containing the EVId, ChargerId, and Time of the event.</param>
    public void HandleEndCharging(EndCharging e)
    {
        if (!_chargerToStation.TryGetValue(e.ChargerId, out var stationId))
            return;

        _stationHandlers[stationId].HandleEndCharging(e);

        if (!_evReservations.ContainsKey(e.EVId))
            throw Log.Error(e.EVId, e.Time, new SkillissueException("Should have a reservation at this point"));

        CancelReservation(e.EVId);
    }

    /// <inheritdoc/>
    public Time ExpectedWaitTime(ushort stationId, Time simNow, Time arrival)
        => GetStationHandler(stationId).ExpectedWaitTime(simNow, arrival);

    private StationHandler GetStationHandler(ushort stationId)
        => _stationHandlers.TryGetValue(stationId, out var handler)
            ? handler
            : throw Log.Error(0, 0,
                new SkillissueException($"Trying to get station handler {stationId} which does not exist."),
                ((string Key, object Value))("StationId", stationId));
}
