namespace Core.Charging.ChargingModel.Chargepoint;

using Core.Shared;

/// <summary>
/// Specifies which side of a dual charging point a vehicle is connected to.
/// </summary>
public enum ChargingSide
{
    Left,
    Right,
}

/// <summary>
/// A charging point with two identical connector sets, allowing two vehicles to charge
/// simultaneously. Both sides support the same socket types — left and right are copies
/// of the same physical connector configuration.
/// </summary>
public interface IDualChargingPoint : IChargingPoint
{
    /// <summary>
    /// Distributes power between two connected vehicles.
    /// Surplus from one side (due to charging curve taper or car rate limit) is
    /// offered to the other, subject to each side's physical connector and car rate limit.
    /// </summary>
    /// <param name="maxKW">The total power available at the charger.</param>
    /// <param name="socA">The state of charge of the vehicle on side A (0.0 to 1.0).</param>
    /// <param name="socB">The state of charge of the vehicle on side B (0.0 to 1.0).</param>
    /// <param name="maxChargeRateKWA">The maximum charge rate of the vehicle on side A.</param>
    /// <param name="maxChargeRateKWB">The maximum charge rate of the vehicle on side B.</param>
    /// <returns>A tuple containing the power allocated to side A and side B in kilowatts.</returns>
    (double PowerA, double PowerB) GetPowerDistribution(
        double maxKW,
        double socA,
        double socB,
        double maxChargeRateKWA,
        double maxChargeRateKWB);

    /// <summary>
    /// Checks if a vehicle with the given socket can connect to either side of the dual charging point.
    /// </summary>
    /// <param name="socket">The socket type the vehicle has.</param>
    /// <returns>The side to which the vehicle can connect, or null if it cannot connect.</returns>
    ChargingSide? CanConnect();

    /// <summary>
    /// Attempts to connect a vehicle with the given socket to the dual charging point.
    /// If the vehicle can connect to either side, it is connected and the method returns the side it was connected to.
    /// </summary>
    /// <param name="socket">The socket type the vehicle has.</param>
    /// <returns>The side to which the vehicle can connect, or null if it cannot connect.</returns>
    ChargingSide? TryConnect();

    /// <summary>
    /// Disconnects a vehicle from the specified side of the dual charging point.
    /// </summary>
    /// <param name="side">The side from which to disconnect the vehicle.</param>
    void Disconnect(ChargingSide side);
}
