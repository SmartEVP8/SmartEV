namespace Core.Vehicles.Configs;

using Core.Shared;

/// <summary>
/// Configuration for batteries.
/// </summary>
/// <param name="minChargeRateKW">The minimum charge rate of the battery in kW.</param>
/// <param name="minCapacityKWh">The minimum capacity of the battery in kWh.</param>
/// <param name="maxCapacityKWh">The maximum capacity of the battery in kWh.</param>
/// <param name="socket">The socket type; determines the maximum charge rate via <see cref="SocketExtensions.PowerKW"/>.</param>
public readonly struct BatteryConfig(ushort minChargeRateKW, ushort minCapacityKWh, ushort maxCapacityKWh, Socket socket)
{
    /// <summary>Gets the minimum charge rate of the battery in kW.</summary>
    public readonly ushort MinChargeRateKW = minChargeRateKW;

    /// <summary>Gets the maximum charge rate of the battery in kW, derived from the socket type.</summary>
    public readonly ushort MaxChargeRateKW { get; } = socket.PowerKW();

    /// <summary>Gets the minimum capacity of the battery in kWh.</summary>
    public readonly ushort MinCapacityKWh = minCapacityKWh;

    /// <summary>Gets the maximum capacity of the battery in kWh.</summary>
    public readonly ushort MaxCapacityKWh = maxCapacityKWh;

    /// <summary>Gets the socket type used for charging.</summary>
    public readonly Socket Socket = socket;
}
