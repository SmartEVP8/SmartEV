namespace Core.Vehicles;

/// <summary>
/// Configuration class for charging decision parameters used by the EVs 
/// when determining whether to search for a charging station 
/// or to re-evaluate an existing reservation.
/// </summary>
public class ChargingDecisionConfig
{
    /// <summary>
    /// Minimum urgency required before an EV starts searching for a station
    /// when it does not already have a reservation.
    /// </summary>
    public double MinimumUrgencyThreshold { get; init; } = 0.3;

    /// <summary>
    /// Minimum urgency required before an EV considers re-evaluating
    /// an existing reservation.
    /// </summary>
    public double ReevaluateUrgency { get; init; } = 0.5;

    /// <summary>
    /// Remaining route distance (KM) below which the EV stops searching
    /// for charging stations.
    /// </summary>
    public double StopSearchDistance { get; init; } = 10.0;

    /// <summary>
    /// Urgency level above which charging search should always be triggered immediately.
    /// </summary>
    public double CriticalUrgency { get; init; } = 0.8;
}