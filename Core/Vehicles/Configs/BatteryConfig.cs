namespace Core.Vehicles.Configs;

using Core.Shared;

/// <summary>
/// Configuration for batteries.
/// </summary>
/// <param name="maxChargeRate">The maximum charge rate of the battery in kW.</param>
/// <param name="maxCapacity">The maximum capacity of the battery in kWh.</param>
/// <param name="socket">The socket type; determines the maximum charge rate via <see cref="SocketExtensions.PowerKW"/>.</param>
public readonly struct BatteryConfig(ushort maxChargeRate, ushort maxCapacity, Socket socket)
{
    /// <summary>The maximum charge rate of the battery in kW.</summary>
    public readonly ushort ChargeRateKW = maxChargeRate;

    /// <summary>The maximum capacity of the battery in kWh.</summary>
    public readonly ushort MaxCapacityKWh = maxCapacity;

    /// <summary>The socket type used for charging.</summary>
    public readonly Socket Socket = socket;
}
