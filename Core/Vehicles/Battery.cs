namespace Core.Vehicles;

using Core.Shared;

/// <summary>
/// Represents the battery of an electric vehicle.
/// </summary>
/// <param name="capacity">The capacity of the battery.</param>
/// <param name="maxChargeRate">The maximum charge rate of the battery.</param>
/// <param name="stateOfCharge">The current state of charge of the battery.</param>
public class Battery(ushort capacity, ushort maxChargeRate, float stateOfCharge)
{
    /// <summary>Gets the capacity of the battery.</summary>
    public ushort MaxCapacityKWh { get; } = capacity;

    /// <summary>Gets the maximum charge rate of the battery.</summary>
    public ushort MaxChargeRateKW { get; } = maxChargeRate;

    /// <summary>Gets or sets the current state of charge of the battery.</summary>
    public float StateOfCharge { get; set; } = stateOfCharge;

    /// <summary>Gets the current usable energy in the battery.</summary>
    public float CurrentChargeKWh => MaxCapacityKWh * StateOfCharge;

    /// <summary>Gets how much capacity remains to be filled.</summary>
    public float RemainingCapacityKWh => MaxCapacityKWh - CurrentChargeKWh;

    /// <summary>True when SoC is at or above the given threshold (default 20 %).</summary>
    /// <param name="thresholdPercent">The threshold percentage to compare the state of charge against.</param>
    /// <returns>If the battery % is above the <paramref name="thresholdPercent"/>.</returns>
    public bool IsAboveThreshold(float thresholdPercent = 0.2f) => StateOfCharge >= thresholdPercent;
}
