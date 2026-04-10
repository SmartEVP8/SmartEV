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
    /// Gets the journey of the EV.
    /// </summary>
    public Journey Journey { get; private set; } = journey;

    /// <summary>
    /// Advances the journey to <paramref name="currentTime"/>, consumes the corresponding energy,
    /// and returns the EV's current position.
    /// </summary>
    /// <param name="currentTime">The simulation time to advance to.</param>
    /// <returns>The EV's current position after advancing.</returns>
    public readonly Position Advance(Time currentTime)
    {
        if (currentTime < Journey.Current.Departure)
            throw new InvalidOperationException($"Cannot advance EV to a time before the current journey's departure (currentTime={currentTime}, departure={Journey.Current.Departure}, {this})");
        var previousJourney = Journey.Current;
        var currentPosition = Journey.AdvanceTo(currentTime);
        ConsumeEnergy(previousJourney, currentTime);
        return currentPosition;
    }

    /// <inheritdoc/>
    public override readonly string ToString() =>
        $"EV(SoC: {Battery.StateOfCharge:P1}, Distance left: {Journey.Current.DistanceKm:F1}km, Energy: {Battery.CurrentChargeKWh:F1}kWh, Efficiency: {ConsumptionWhPerKm}Wh/km)";

    /// <summary>
    /// Whether the EV can complete its current journey and still have at least
    /// <paramref name="minAcceptableCharge"/> SoC remaining on arrival.
    /// </summary>
    /// <param name="timeAtStation">Time spent at a station.</param>
    /// <param name="minAcceptableCharge">Minimum SoC required on arrival. Defaults to 0.1.</param>
    /// <returns>True if the EV can complete its current journey with the specified reserve; otherwise, false.</returns>
    public readonly bool CanCompleteJourney(Time? timeAtStation = null, float minAcceptableCharge = 0.1f)
    {
        if (timeAtStation != null) Journey.UpdateRouteToDestination(timeAtStation.Value);
        return CanReach(Journey.Current.DistanceKm, minAcceptableCharge);
    }

    /// <summary>
    /// Whether the EV can reach a point <paramref name="distanceKm"/> away and still
    /// have at least <paramref name="minAcceptableCharge"/> SoC remaining on arrival.
    /// </summary>
    /// <param name="distanceKm">Distance to the target in km.</param>
    /// <param name="minAcceptableCharge">Minimum SoC required on arrival. Defaults to 0.1.</param>
    /// <returns>True if the EV can reach the target with the specified reserve; otherwise, false.</returns>
    private readonly bool CanReach(float distanceKm, float minAcceptableCharge = 0.1f)
    {
        var reserveKWh = Battery.MaxCapacityKWh * minAcceptableCharge;
        var usableKWh = Battery.CurrentChargeKWh - reserveKWh;
        return EnergyForDistanceKWh(distanceKm) <= usableKWh;
    }

    /// <summary>
    /// Whether the EV can tolerate a detour that goes through a station and still
    /// keep at least <paramref name="minAcceptableCharge"/> SoC on arrival.
    /// </summary>
    /// <param name="detourDistanceKm">The total detour distance from the current position to the destination via the station.</param>
    /// <param name="directDistanceKm">The direct current-route distance from the current position to the destination.</param>
    /// <param name="minAcceptableCharge">Minimum SoC required on arrival. Defaults to 0.1.</param>
    /// <returns>True if the implied station leg is reachable with the specified reserve; otherwise, false.</returns>
    public readonly bool CanReachViaDetour(float detourDistanceKm, float directDistanceKm, float minAcceptableCharge = 0.1f)
    {
        var inferredStationLegKm = Math.Max(0f, detourDistanceKm - directDistanceKm);
        return CanReach(inferredStationLegKm, minAcceptableCharge);
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
        if (Battery.MaxCapacityKWh == 0)
            throw new InvalidOperationException($"Battery capacity must be greater than zero when calculating desired SoC (arrivalAtStation={arrivalAtStation}, {this})");

        var remainingDistanceKm = Journey.RemainingDistanceToDestination(arrivalAtStation);
        var energyToDest = EnergyForDistanceKWh(remainingDistanceKm);
        var percentNeededToDestination = energyToDest / Battery.MaxCapacityKWh;
        if (!float.IsFinite(percentNeededToDestination) || percentNeededToDestination < 0f || float.IsNaN(percentNeededToDestination))
            throw new InvalidOperationException($"Calculated percent needed to destination is invalid (percentNeededToDestination={percentNeededToDestination}, arrivalAtStation={arrivalAtStation}, {this})");
        var chargeToPercent = percentNeededToDestination + Preferences.MinAcceptableCharge;
        var desiredSoC = chargeToPercent > 1f ? 0.8f : chargeToPercent;
        return Math.Clamp(desiredSoC + 0.01f, 0f, 1f);
    }

    /// <summary>
    /// Calculates the next time to check for candidate stations, which is the minimum of:
    /// 1) The time it takes to reach halfway to the next stop, and
    /// 2) The time it takes to reach half of the remaining battery.
    /// </summary>
    /// <param name="currentTime">The current simulation time.</param>
    /// <returns>The Time the next FindCandidateStation Event should occur.</returns>
    public readonly Time TimeToNextFindCandidateCheck(Time currentTime) => Math.Min(Journey.TimeToReachHalfToNextStop(), TimeUntilHalfOfBattery(currentTime));

    private readonly Time TimeUntilHalfOfBattery(Time currentTime)
    {
        if (Preferences.MinAcceptableCharge >= Battery.StateOfCharge)
        {
            if (currentTime < Journey.Current.Departure)
                throw new InvalidOperationException($"Current time is before the departure of the current journey (currentTime={currentTime}, departure={Journey.Current.Departure}, {this})");
            return currentTime;
        }

        var percent = Math.Max(Preferences.MinAcceptableCharge, Battery.StateOfCharge / 2);
        var acceptableKWh = Battery.MaxCapacityKWh * percent;
        var distanceAtHalfBattery = acceptableKWh / (ConsumptionWhPerKm / 1000f);
        return Journey.Current.Departure + Journey.TimeToDriveDistance(distanceAtHalfBattery);
    }

    /// <summary>
    /// Consumes energy based on the distance traveled along the supplied journey snapshot.
    /// </summary>
    /// <param name="journey">The journey snapshot before the advance.</param>
    /// <param name="currentTime">The ending time of the interval which to calculate energy consumption.</param>
    private readonly void ConsumeEnergy(CurrentJourney journey, Time currentTime)
    {
        if (journey.Duration.Milliseconds == 0)
            return;

        var fractionTraveled = (currentTime - journey.Departure) / (double)journey.Duration;
        fractionTraveled = Math.Clamp(fractionTraveled, 0d, 1d);
        var distanceKm = journey.DistanceKm * fractionTraveled;
        var energyKWh = EnergyForDistanceKWh((float)distanceKm);
        var socLost = energyKWh / Battery.MaxCapacityKWh;
        Battery.StateOfCharge = Math.Clamp(Battery.StateOfCharge - socLost, 0f, 1f);
    }

    /// <summary>Calculates the energy required to travel <paramref name="distanceKm"/>.</summary>
    private readonly float EnergyForDistanceKWh(float distanceKm) =>
        distanceKm * ConsumptionWhPerKm / 1000f;
}
