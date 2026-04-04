namespace Core.Vehicles;

using Core.Routing;
using Core.Shared;

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
    /// Gets or sets the scheduler token for the currently planned arrival event.
    /// </summary>
    public uint? ScheduledArrivalEventToken { get; set; }

    /// <summary>
    /// Gets the journey of the EV.
    /// </summary>
    public Journey Journey { get; private set; } = journey;

    /// <inheritdoc/>
    public override readonly string ToString() =>
        $"EV(SoC: {Battery.StateOfCharge:P1}, Distance left: {Journey.Current.DistanceKm:F1}km, Energy: {Battery.CurrentChargeKWh:F1}kWh, Efficiency: {ConsumptionWhPerKm}Wh/km)";

    /// <summary>
    /// Whether the EV can complete its current journey and still have at least
    /// <paramref name="reserve"/> SoC remaining on arrival.
    /// </summary>
    /// <param name="reserve">Minimum SoC required on arrival. Defaults to 0.1.</param>
    /// <returns>True if the EV can complete its current journey with the specified reserve; otherwise, false.</returns>
    public readonly bool CanCompleteJourney(float reserve = 0.1f) =>
        CanReach(Journey.Current.DistanceKm, reserve);

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
    /// Calculates how much an EV needs to charge to reach its
    /// destination while having at least their minimunAcceptable charge.
    /// If they cant reach the destination with a single charge
    /// they should charge to 80%.
    /// </summary>
    /// <param name="arrivalAtStation">The time right now.</param>
    /// <returns>Returns the percentence that the EV should charge to.</returns>
    public readonly float CalcDesiredSoC(Time arrivalAtStation)
    {
        var percentageCompleted = (arrivalAtStation - Journey.Current.Departure) / (double)Journey.Current.Duration;
        var remainingDistanceKm = Journey.Current.DistanceKm * (1.0 - percentageCompleted);

        var energyToDest = EnergyForDistanceKWh((float)remainingDistanceKm);
        var percentNeededToDestination = energyToDest / Battery.MaxCapacityKWh;
        var chargeToPercent = percentNeededToDestination + Preferences.MinAcceptableCharge;

        return chargeToPercent > 1f ? 0.8f : chargeToPercent;
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
        var fractionTraveled = (to - from) / (double)Journey.Current.Duration;
        var distanceKm = Journey.Current.DistanceKm * fractionTraveled;
        var energyKWh = EnergyForDistanceKWh((float)distanceKm);
        var socLost = energyKWh / Battery.MaxCapacityKWh;
        Battery.StateOfCharge = Math.Clamp(Battery.StateOfCharge - socLost, 0f, 1f);
    }

    /// <summary>Calculates the energy required to travel <paramref name="distanceKm"/>.</summary>
    private readonly float EnergyForDistanceKWh(float distanceKm) =>
        distanceKm * ConsumptionWhPerKm / 1000f;
}
