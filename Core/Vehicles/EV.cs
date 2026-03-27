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
    /// Gets the efficiency rating of the EV.
    /// </summary>
    public ushort Efficiency { get; } = efficiency;

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
}
