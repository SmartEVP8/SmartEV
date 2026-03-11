namespace Core.Charging;

/// <summary>
/// Represents the result of a charging session, including the time required to reach 
/// target state of charge for one or two batteries.
/// </summary>
/// <param name="TimeHours1">The time required to reach the target state of charge for the first battery.</param>
/// <param name="TimeHours2">The time required to reach the target state of charge for the second battery.</param>
public record ChargingEstimate(double TimeHours1, double TimeHours2);