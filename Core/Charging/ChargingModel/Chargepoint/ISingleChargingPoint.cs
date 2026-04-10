namespace Core.Charging.ChargingModel.Chargepoint;

using Core.Shared;

/// <summary>
/// Represents a charging point that supports a single vehicle connection.
/// </summary>
public interface ISingleChargingPoint : IChargingPoint
{
    /// <summary>
    /// Returns the power delivered to the single connected vehicle,
    /// capped by both <paramref name="maxKW"/> and the connector's rated power.
    /// </summary>
    /// <param name="maxKW">The maximum power in kilowatts the charger can deliver.</param>
    /// <param name="soc">State of charge of the connected vehicle (0.0 to 1.0).</param>
    /// <returns>The power output in kilowatts delivered to the connected vehicle.</returns>
    double GetPowerOutput(double maxKW, double soc);

    /// <summary>
    /// Checks if a vehicle with the given socket can connect to the charging point.
    /// </summary>
    /// <param name="socket">The socket to check.</param>
    /// <returns>true if the socket is compatible and the charging point is free.</returns>
    bool CanConnect();

    /// <summary>
    /// Attempts to connect a vehicle with the given socket to the charging point.
    /// </summary>
    /// <param name="socket">The socket to connect.</param>
    /// <returns>true if the connection was successful.</returns>
    bool TryConnect();

    /// <summary>
    /// Disconnects the currently connected vehicle from the charging point.
    /// </summary>
    void Disconnect();
}
