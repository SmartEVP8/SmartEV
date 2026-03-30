namespace Core.Vehicles;

using Core.Routing;
using Core.Shared;
using Core.GeoMath;

/// <summary>
/// Represents an electric vehicle (EV) with a battery, preferences, a journey, and an efficiency rating.
/// </summary>
/// <param name="battery">The battery of the EV.</param>
/// <param name="preferences">The preferences of the EV.</param>
/// <param name="journey">The journey of the EV.</param>
/// <param name="efficiency">The efficiency rating of the EV.</param>
public struct EV(Battery battery, Preferences preferences, Journey journey, ushort efficiency)
{
    /// <summary>
    /// Gets the preferences of the EV.
    /// </summary>
    public Preferences Preferences { get; } = preferences;

    /// <summary>
    /// Gets the battery of the EV.
    /// </summary>
    public Battery Battery { get; } = battery;

    /// <summary>
    /// Gets the energy consumption of this model in Wh/km.
    /// </summary>
    public ushort ConsumptionWhPerKm { get; } = efficiency;

    /// <summary>
    /// Gets or sets a reservation at a station for the EV.
    /// </summary>
    public ushort? HasReservationAtStationId { get; set; }

    /// <summary>
    /// Gets the journey of the EV.
    /// </summary>
    public Journey Journey { get; private set; } = journey;

    /// <summary>
    /// Gets or sets a value indicating whether check if the EV is charging.
    /// </summary>
    public bool IsCharging { get; set; } = false;

    /// <summary>
    /// Determines whether the EV has departed based on the current time
    /// and the departure time of its journey.
    /// </summary>
    /// <param name="currentTime">The current time to compare against the EV's departure time.</param>
    /// <returns>True if the EV has departed; otherwise, false.</returns>
    public readonly bool HasDeparted(Time currentTime) => Journey.JourneyStart <= currentTime;

    /// <summary>
    /// Determines whether the EV has arrived at its destination based on the current time.
    /// </summary>
    /// <param name="currentTime">Timestamp.</param>
    /// <returns>True of false based on if the EV has arrived at its journeys end.</returns>
    public readonly bool HasArrived(Time currentTime) => Journey.JourneyStart + Journey.LastUpdatedDuration <= currentTime;


    /// <inheritdoc/>
    public override readonly string ToString() =>
        $"EV(SoC: {Battery.StateOfCharge:P1}, Distance left: {Journey.LastUpdatedDistancekm:F1}km, Energy: {Battery.CurrentChargeKWh:F1}kWh, Efficiency: {ConsumptionWhPerKm}Wh/km)";

    /// <summary>
    /// Whether the EV can complete its current journey and still have at least
    /// <paramref name="reserve"/> SoC remaining on arrival.
    /// </summary>
    /// <param name="reserve">Minimum SoC required on arrival. Defaults to 0.1.</param>
    /// <returns>True if the EV can complete its current journey with the specified reserve; otherwise, false.</returns>
    public readonly bool CanCompleteJourney(float reserve = 0.1f) =>
        CanReach(Journey.LastUpdatedDistancekm, reserve);

    /// <summary>
    /// Whether the EV can reach a point <paramref name="distanceKm"/> away and still
    /// have at least <paramref name="reserve"/> SoC remaining on arrival.
    /// </summary>
    /// <param name="distanceKm">Distance to the target in km.</param>
    /// <param name="reserve">Minimum SoC required on arrival. Defaults to 0.1.</param>
    /// <returns>True if the EV can reach the target with the specified reserve; otherwise, false.</returns>
    public readonly bool CanReach(float distanceKm, float reserve = 0.1f)
    {
        var reserveKWh = Battery.MaxCapacityKWh * reserve;
        var usableKWh = Battery.CurrentChargeKWh - reserveKWh;
        return EnergyForDistanceKWh(distanceKm) <= usableKWh;
    }

    /// <summary>
    /// Consumes energy based on the distance traveled between <paramref name="from"/> and <paramref name="to"/>.
    /// </summary>
    /// <param name="from">The starting time of the interval which to calculate energy consumption.
    /// Should be between the journey's departure and arrival times.</param>
    /// <param name="to">The ending time of the interval which to calculate energy consumption.
    /// Should be between the journey's departure and arrival times, and greater than <paramref name="from"/>.</param>
    public readonly void ConsumeEnergy(Time from, Time to)
    {
        var fractionTraveled = (to - from) / (double)Journey.LastUpdatedDuration;
        var distanceKm = Journey.LastUpdatedDistancekm * fractionTraveled;
        var energyKWh = EnergyForDistanceKWh((float)distanceKm);
        var socLost = energyKWh / Battery.MaxCapacityKWh;
        Battery.StateOfCharge = Math.Clamp(Battery.StateOfCharge - socLost, 0f, 1f);
    }

    /// <summary>Calculates the energy required to travel <paramref name="distanceKm"/>.</summary>
    private readonly float EnergyForDistanceKWh(float distanceKm) =>
        distanceKm * ConsumptionWhPerKm / 1000f;

    public float ChargeTo(Time time)
    {
        var distanceToDest = GeoMath.EquirectangularDistance(Journey.Path.Waypoints[^1], Journey.CurrentPosition(time));
        var energyToDest = EnergyForDistanceKWh((float)distanceToDest);
        var percentNeededToDestination = energyToDest / Battery.MaxCapacityKWh;
        var chargeToPercent = percentNeededToDestination + Preferences.MinAcceptableCharge;
        if (chargeToPercent > 1f)
        {
            return 0.8f;
        }

        return chargeToPercent;
    }
}
