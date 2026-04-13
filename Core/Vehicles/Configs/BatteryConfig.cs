namespace Core.Vehicles.Configs;

/// <summary>
/// Configuration for batteries.
/// </summary>
/// <param name="maxChargeRate">The maximum charge rate of the battery in kW.</param>
/// <param name="maxCapacity">The maximum capacity of the battery in kWh.</param>
public readonly struct BatteryConfig(ushort maxChargeRate, ushort maxCapacity)
{
    /// <summary>The maximum charge rate of the battery in kW.</summary>
    public readonly ushort ChargeRateKW = maxChargeRate;

    /// <summary>The maximum capacity of the battery in kWh.</summary>
    public readonly ushort MaxCapacityKWh = maxCapacity;
}
