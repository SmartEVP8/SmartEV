namespace Core.Charging;

using Core.Shared;
using Core.Vehicles;

/// <summary>
/// SingleChargingPoint represents a charging point with a single connector, allowing for one electric vehicle to charge at a time.
/// </summary>
public readonly struct SingleChargingPoint : IChargingPoint
{
    private readonly List<Connector> _connectors;

    /// <inheritdoc/>
    public List<Socket> GetSockets() => _connectors.Select(c => c.GetSocket()).Distinct().ToList();

    /// <summary>
    /// Allocates available power to the single connected battery and computes the expected charging time.
    /// </summary>
    /// <param name="chargingModel"> The charging model to use for computing charging time. </param>
    /// <param name="availablePower"> The total power available for allocation. </param>
    /// <param name="socTarget"> The target state of charge for the battery. </param>
    /// <param name="battery"> The snapshot of the battery's current state. </param>
    /// <returns> The result of the allocation and computation. </returns>
    public ChargingEstimate AllocateAndCompute(
        ChargingModel chargingModel,
        double availablePower,
        double socTarget,
        GetBattery battery)
    {
        var allocation = PowerDistributor.DistributeSingle(availablePower, battery.MaxChargeRate);

        var time1 = chargingModel.GetChargingTimeHours(
            battery.CurrentCharge, socTarget, battery.Capacity, allocation.Allocated1);

        return new ChargingEstimate(time1, 0.0);
    }
}
