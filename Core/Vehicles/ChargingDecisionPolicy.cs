namespace Core.Vehicles;

/// <summary>
/// Defines decision rules based on charging urgency and remaining route distance
/// for whether an EV should search for charging, consider re-evaluating an
/// existing reservation, or stop considering charging.
/// </summary>
public static class ChargingDecisionPolicy
{
    /// <summary>
    /// Determines whether the EV should stop searching for charging stations
    /// based on the remaining route distance.
    /// </summary>
    /// <param name="remainingDistanceKm">
    /// The remaining distance on the route, expressed in kilometers.
    /// </param>
    /// <param name="config">
    /// The charging decision configuration.
    /// </param>
    /// <returns>
    /// True if the EV should stop searching for charging stations; otherwise, false.
    /// </returns>
    public static bool ShouldStopSearching(
        double remainingDistanceKm,
        ChargingDecisionConfig config)
    {
        return remainingDistanceKm <= config.StopSearchDistance;
    }

    /// <summary>
    /// Determines whether the EV should start searching for charging stations
    /// based on charging urgency and remaining route distance.
    /// </summary>
    /// <param name="stateOfCharge">
    /// The current state of charge of the EV, expressed as a percentage.
    /// </param>
    /// <param name="minAcceptableCharge">
    /// The minimum acceptable charge level, expressed as a percentage.
    /// </param>
    /// <param name="remainingDistanceKm">
    /// The remaining distance on the route, expressed in kilometers.
    /// </param>
    /// <param name="config">
    /// The charging decision configuration.
    /// </param>
    /// <returns>
    /// True if the EV should start searching for charging stations; otherwise, false.
    /// </returns>
    public static bool ShouldSearchForStation(
        float stateOfCharge,
        float minAcceptableCharge,
        double remainingDistanceKm,
        ChargingDecisionConfig config)
    {
        if (ShouldStopSearching(remainingDistanceKm, config))
        {
            return false;
        }

        double urgency = Urgency.CalculateChargeUrgency(stateOfCharge, minAcceptableCharge);
        return urgency >= config.MinimumUrgencyThreshold;
    }

    /// <summary>
    /// Determines whether the EV should consider re-evaluating an existing reservation
    /// based on charging urgency and remaining route distance.
    /// </summary>
    /// <param name="stateOfCharge">
    /// The current state of charge of the EV, expressed as a percentage.
    /// </param>
    /// <param name="minAcceptableCharge">
    /// The minimum acceptable charge level, expressed as a percentage.
    /// </param>
    /// <param name="remainingDistanceKm">
    /// The remaining distance on the route, expressed in kilometers.
    /// </param>
    /// <param name="config">
    /// The charging decision configuration.
    /// </param>
    /// <returns>
    /// True if the EV should consider re-evaluating an existing reservation; otherwise, false.
    /// </returns>
    public static bool ShouldReevaluateReservation(
        float stateOfCharge,
        float minAcceptableCharge,
        double remainingDistanceKm,
        ChargingDecisionConfig config)
    {
        if (ShouldStopSearching(remainingDistanceKm, config))
        {
            return false;
        }

        double urgency = Urgency.CalculateChargeUrgency(stateOfCharge, minAcceptableCharge);
        return urgency >= config.ReevaluateUrgency;
    }

    /// <summary>
    /// Determines whether the EV should immediately search for charging stations
    /// based on charging urgency and remaining route distance.
    /// </summary>
    /// <param name="stateOfCharge">
    /// The current state of charge of the EV, expressed as a percentage.
    /// </param>
    /// <param name="minAcceptableCharge">
    /// The minimum acceptable charge level, expressed as a percentage.
    /// </param>
    /// <param name="remainingDistanceKm">
    /// The remaining distance on the route, expressed in kilometers.
    /// </param>
    /// <param name="config">
    /// The charging decision configuration.
    /// </param>
    /// <returns>
    /// True if the EV should immediately search for charging stations; otherwise, false.
    /// </returns>
    public static bool ShouldTriggerImmediateSearch(
        float stateOfCharge,
        float minAcceptableCharge,
        double remainingDistanceKm,
        ChargingDecisionConfig config)
    {
        if (ShouldStopSearching(remainingDistanceKm, config))
        {
            return false;
        }

        double urgency = Urgency.CalculateChargeUrgency(stateOfCharge, minAcceptableCharge);
        return urgency >= config.CriticalUrgency;
    }
}