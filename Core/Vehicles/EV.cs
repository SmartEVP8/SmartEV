namespace Core.Vehicles;

using Core.Routing;
using Core.Shared;
using Serilog;

/// <summary>
/// Defines the possible states of an EV.
/// </summary>
public enum EVState
{
    Driving,
    Queueing,
    Charging,
}

/// <summary>
/// Represents an electric vehicle (EV) with a battery, preferences, a journey, and an efficiency rating.
/// </summary>
/// <param name="id">The EV id.</param>
/// <param name="battery">The battery of the EV.</param>
/// <param name="preferences">The preferences of the EV.</param>
/// <param name="journey">The journey of the EV.</param>
/// <param name="efficiency">The efficiency rating of the EV.</param>
public class EV(int id, Battery battery, Preferences preferences, Journey journey, ushort efficiency)
{
    /// <summary>
    /// Gets the Id of the EV.
    /// </summary>
    public int Id { get; } = id;

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
    /// Gets the journey of the EV.
    /// </summary>
    public Journey Journey { get; } = journey;

    /// <summary>
    /// Gets or sets a value indicating whether the EV should drive directly to its destination without charging.
    /// </summary>
    public bool DriveDirectlyToDestination { get; set; } = false;

    /// <summary>
    /// Gets or sets the current state of the EV.
    /// </summary>
    public EVState EVState { get; set; } = EVState.Driving;

    /// <summary>
    /// Advances the journey to <paramref name="currentTime"/>, consumes the corresponding energy,
    /// and returns the EV's current position.
    /// </summary>
    /// <param name="currentTime">The simulation time to advance to.</param>
    /// <returns>The EV's current position after advancing.</returns>
    public Position Advance(Time currentTime)
    {
        if (currentTime < Journey.Current.Departure)
        {
            var ex = new InvalidOperationException($"Cannot advance EV to a time before the current journey's departure (currentTime={currentTime}, departure={Journey.Current.Departure}, {this})");
            Log.Error(ex, "Invalid advance time: {CurrentTime} is before journey departure: {Departure}. {EV}", currentTime, Journey.Current.Departure, this);
            throw ex;
        }

        var previousJourney = Journey.Current;
        var currentPosition = Journey.AdvanceTo(currentTime);
        ConsumeEnergy(previousJourney, currentTime);
        return currentPosition;
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"EV(SoC: {Battery.StateOfCharge:P1}, Distance left: {Journey.Current.DistanceKm:F1}km, Energy: {Battery.CurrentChargeKWh:F1}kWh, Efficiency: {ConsumptionWhPerKm}Wh/km)";

    /// <summary>
    /// Whether the EV can complete its current journey and still have at least
    /// <paramref name="minAcceptableCharge"/> SoC remaining on arrival.
    /// </summary>
    /// <param name="timeAtStation">Time spent at a station.</param>
    /// <param name="minAcceptableCharge">Minimum SoC required on arrival. Defaults to 0.1.</param>
    /// <returns>True if the EV can complete its current journey with the specified reserve; otherwise, false.</returns>
    public bool CanCompleteJourney(Time? timeAtStation = null, float minAcceptableCharge = 0.1f)
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
    private bool CanReach(float distanceKm, float minAcceptableCharge = 0.1f)
    {
        var reserveKWh = Battery.MaxCapacityKWh * minAcceptableCharge;
        var usableKWh = Battery.CurrentChargeKWh - reserveKWh;
        return EnergyForDistanceKWh(distanceKm) <= usableKWh;
    }

    /// <summary>
    /// Whether the EV can reach a station and still keep at least
    /// <paramref name="minAcceptableCharge"/> SoC on arrival.
    /// </summary>
    /// <param name="distanceToStationKm">Distance from the current position to the station in km.</param>
    /// <param name="minAcceptableCharge">Minimum SoC required on arrival. Defaults to 0.1.</param>
    /// <returns>True if the station leg is reachable with the specified reserve; otherwise, false.</returns>
    public bool CanReachToStation(float distanceToStationKm, float minAcceptableCharge = 0.1f) => CanReach(distanceToStationKm, minAcceptableCharge);

    /// <summary>
    /// Calculates how much an EV needs to charge to reach its
    /// destination while having at least their minimunAcceptable charge.
    /// If they cant reach the destination with a single charge
    /// they should charge to 80%.
    /// </summary>
    /// <param name="arrivalAtStation">The time right now.</param>
    /// <returns>Returns the percentence that the EV should charge to.</returns>
    public float CalcDesiredSoC(Time arrivalAtStation)
    {
        if (Battery.MaxCapacityKWh == 0)
        {
            var ex2 = new InvalidOperationException($"Battery capacity must be greater than zero when calculating desired SoC (arrivalAtStation={arrivalAtStation}, {this})");
            Log.Error(ex2, "Battery capacity must be greater than zero when calculating desired SoC. ArrivalAtStation={ArrivalAtStation}, {EV}", arrivalAtStation, this);
            throw ex2;
        }
        var remainingDistanceKm = Journey.RemainingDistanceToDestination(arrivalAtStation);
        var energyToDest = EnergyForDistanceKWh(remainingDistanceKm);
        var percentNeededToDestination = energyToDest / Battery.MaxCapacityKWh;
        var chargeToPercent = percentNeededToDestination + Preferences.MinAcceptableCharge;
        var desiredSoC = chargeToPercent > 1f ? 0.8f : chargeToPercent;

        if (float.IsFinite(desiredSoC))
            return Math.Clamp(desiredSoC + 0.01f, 0f, 1f);

        var ex = new InvalidOperationException($"Calculated desired SoC is not finite (desiredSoC={desiredSoC}, energyToDest={energyToDest}, remainingDistanceKm={remainingDistanceKm}, arrivalAtStation={arrivalAtStation}, {this})");
        Log.Error(ex, "Calculated desired SoC is not finite. DesiredSoC={DesiredSoC}, EnergyToDestinationKWh={EnergyToDest}, RemainingDistanceKm={RemainingDistanceKm}, ArrivalAtStation={ArrivalAtStation}, {EV}",
            desiredSoC, energyToDest, remainingDistanceKm, arrivalAtStation, this);
        throw ex;
    }

    /// <summary>
    /// Calculates the expected amount an EV needs to charge at a station.
    /// </summary>
    /// <param name="distanceKM">The distance an EV needs to drive after charging.</param>
    /// <returns>Returns the expected SoC an EV should charge to.</returns>
    public float PreCalculatedTargetSoC(float distanceKM)
    {
        if (Battery.MaxCapacityKWh <= 0)
        {
            var ex2 = new InvalidOperationException($"Battery capacity must be greater than zero when calculating pre-calculated target SoC (distanceKM={distanceKM}, {this})");
            Log.Error(ex2, "Battery capacity must be greater than zero when calculating pre-calculated target SoC. DistanceKM={DistanceKM}, {EV}", distanceKM, this);
            throw ex2;
        }

        var energyToDestinationKWh = EnergyForDistanceKWh(distanceKM);

        var energyNeededToDestKWh = (Preferences.MinAcceptableCharge * Battery.MaxCapacityKWh) + energyToDestinationKWh;

        var chargeToPercent = energyNeededToDestKWh / Battery.MaxCapacityKWh;
        var desiredSoC = chargeToPercent > 1f ? 0.8f : chargeToPercent;

        if (float.IsFinite(desiredSoC))
            return Math.Clamp(desiredSoC + 0.01f, 0f, 1f);

        var ex = new InvalidOperationException($"Calculated desired SoC is not finite (desiredSoC={desiredSoC}, energyToDestinationKWh={energyToDestinationKWh}, distanceKM={distanceKM}, {this})");
        Log.Error(ex, "Calculated desired SoC is not finite. DesiredSoC={DesiredSoC}, EnergyToDestinationKWh={EnergyToDestinationKWh}, DistanceKM={DistanceKM}, {EV}",
            desiredSoC, energyToDestinationKWh, distanceKM, this);
        throw ex;
    }

    /// <summary>
    /// Calculates the amount of SoC should have been used within a time interval.
    /// </summary>
    /// <param name="duration">The time the EV has to drive in.</param>
    /// <returns>Returns how much SoC an EV has after a Time interval.</returns>
    public float EstimateSoCAfterADuration(Time duration)
    {
        if (Battery.MaxCapacityKWh <= 0)
        {
            var ex = new InvalidOperationException($"Battery capacity must be greater than zero when estimating SoC after a duration (duration={duration}, {this})");
            Log.Error(ex, "Battery capacity must be greater than zero when estimating SoC after a duration. Duration={Duration}, {EV}", duration, this);
            throw ex;
        }

        var journey = Journey.Original;

        var avgSpeedKmMs = journey.DistanceKm / journey.Duration.Milliseconds;
        var distanceTraveledKm = avgSpeedKmMs * duration.Milliseconds;
        var energyNeededKWh = EnergyForDistanceKWh(distanceTraveledKm);
        var socDrop = energyNeededKWh / Battery.MaxCapacityKWh / 100;
        return Battery.StateOfCharge - socDrop;
    }

    /// <summary>Estimates the SoC when reaching the next stop.</summary>
    /// <returns>The projected SoC at arrival to the next stop.</returns>
    public float EstimateSoCAtNextStop()
    {
        if (Battery.MaxCapacityKWh <= 0)
        {
            var ex = new InvalidOperationException($"Battery capacity must be greater than zero when estimating SoC at next stop (Journey.Current={Journey.Current}, {this})");
            Log.Error(ex, "Battery capacity must be greater than zero when estimating SoC at next stop. JourneyCurrent={JourneyCurrent}, {EV}", Journey.Current, this);
            throw ex;
        }

        var journey = Journey.Current;

        if (journey.Duration == 0)
            return Battery.StateOfCharge;

        var energyNeededKWh = EnergyForDistanceKWh(journey.DistanceToNextStopKm);
        var socDrop = energyNeededKWh / Battery.MaxCapacityKWh;
        return Math.Clamp(Battery.StateOfCharge - socDrop, 0f, 1f);
    }

    public float CalcPreDesiredComputedSoC(float distanceToDestination)
    {
        if (Battery.MaxCapacityKWh <= 0)
        {
            var ex2 = new InvalidOperationException($"Battery capacity must be greater than zero when calculating pre-desired computed SoC (distanceToDestination={distanceToDestination}, {this})");
            Log.Error(ex2, "Battery capacity must be greater than zero when calculating pre-desired computed SoC. DistanceToDestination={DistanceToDestination}, {EV}", distanceToDestination, this);
            throw ex2;
        }
        var energyToDestinationKWh = EnergyForDistanceKWh(distanceToDestination);

        var energyNeededToDest = (Preferences.MinAcceptableCharge * Battery.MaxCapacityKWh) + energyToDestinationKWh;

        var chargeToPercent = energyNeededToDest / Battery.MaxCapacityKWh;
        var desiredSoC = chargeToPercent > 1f ? 0.8f : chargeToPercent;

        if (float.IsFinite(desiredSoC))
            return Math.Clamp(desiredSoC + 0.01f, 0f, 1f);

        var ex = new InvalidOperationException($"Calculated desired SoC is not finite (desiredSoC={desiredSoC}, energyNeededToDest={energyNeededToDest}, distanceToDestination={distanceToDestination}, {this})");
        Log.Error(ex, "Calculated desired SoC is not finite. DesiredSoC={DesiredSoC}, EnergyNeededToDestinationKWh={EnergyNeededToDest}, DistanceToDestination={DistanceToDestination}, {EV}",
            desiredSoC, energyNeededToDest, distanceToDestination, this);
        throw ex;
    }

    /// <summary>
    /// Calculate how far an EV can drive on current charge in km.
    /// </summary>
    /// <returns>Distance in km that a EV can drive.</returns>
    public float DistanceOnCurrentChargeKm() => Battery.CurrentChargeKWh / (ConsumptionWhPerKm / 1000f);

    /// <summary>
    /// Calculates how far an EV can drive in a given time based on its current route's average speed.
    /// </summary>
    /// <param name="time">The amount of time an EV has to drive.</param>
    /// <returns>Returns the distance an EV can drive in a time period.</returns>
    public float DistanceEVCanDrive(Time time) => journey.Current.DistanceKm / journey.Current.Duration.Milliseconds * time.Milliseconds;

    /// <summary>
    /// Calculates the next time to check for candidate stations, which is the minimum of:
    /// 1) The time it takes to reach halfway to the next stop, and
    /// 2) The time it takes to reach half of the remaining battery.
    /// </summary>
    /// <param name="currentTime">The current simulation time.</param>
    /// <returns>The Time the next FindCandidateStation Event should occur.</returns>
    public Time TimeAtNextFindCandidateCheck(Time currentTime) => Math.Min(Journey.TimeToReachHalfToNextStop(), TimeUntilHalfOfBattery(currentTime));

    private Time TimeUntilHalfOfBattery(Time currentTime)
    {
        if (Preferences.MinAcceptableCharge >= Battery.StateOfCharge)
        {
            if (currentTime < Journey.Current.Departure)
            {
                var ex = new InvalidOperationException($"Current time is before the departure of the current journey (currentTime={currentTime}, departure={Journey.Current.Departure}, {this})");
                Log.Error(ex, "Invalid time for battery check: {CurrentTime} is before journey departure: {Departure}. {EV}", currentTime, Journey.Current.Departure, this);
                throw ex;
            }

            return Journey.Current.Departure + 1;
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
    private void ConsumeEnergy(CurrentJourney journey, Time currentTime)
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
    /// <param name="distanceKm">The distance in km for which to calculate energy consumption.</param>
    /// <returns>The energy in kWh required to travel the specified distance.</returns>
    public float EnergyForDistanceKWh(float distanceKm) =>
        distanceKm * ConsumptionWhPerKm / 1000f;

    /// <summary>
    /// Calculates how much SoC it take to drive a <paramref name="distanceKm"/>.
    /// </summary>
    /// <param name="distanceKm">The distance in km for which to calculate SoC consumptions.</param>
    /// <returns>The SoC used for driving the specified distance.</returns>
    public float SoCUsedAfterADistance(float distanceKm) =>
        (Battery.CurrentChargeKWh - EnergyForDistanceKWh(distanceKm)) / Battery.MaxCapacityKWh;

    /// <summary>
    /// Checks if the EV can reach a station without exceeding a certain state of charge threshold at the station, which is based on the distance to the station and the distance to the destination.
    /// </summary>
    /// <param name="distToStation">The distance to a station from its current position.</param>
    /// <param name="distToDestination">The distance to an EV's destination from a station.</param>
    /// <param name="chargeBufferPercent">A buffer to account for noise.</param>
    /// <returns>Returns a bool representing if a EV would arrive with more SoC than it needs to charge to.</returns>
    public bool CheckIfTargetSoCIsLowerThanCurrentSoC(float distToStation, float distToDestination, float chargeBufferPercent)
    {
        var socAtStation = (Battery.CurrentChargeKWh - EnergyForDistanceKWh(distToStation / 1000)) / Battery.MaxCapacityKWh;
        var expectChargeTarget = PreCalculatedTargetSoC(distToDestination / 1000) * chargeBufferPercent;

        var socAtDestination = socAtStation - EnergyForDistanceKWh(distToDestination / 1000) / Battery.MaxCapacityKWh;
        var canReachDestWithMinCharge = socAtDestination >= Preferences.MinAcceptableCharge;

        return socAtStation > expectChargeTarget && canReachDestWithMinCharge;
    }
}
