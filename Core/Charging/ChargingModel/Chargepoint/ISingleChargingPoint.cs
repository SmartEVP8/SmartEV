namespace Core.Charging.ChargingModel.Chargepoint;

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
}