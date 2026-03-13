namespace Core.Charging;

using Core.Shared;
using Core.Vehicles;

/// <summary>
/// DualChargingPoint represents a charging point with two connectors, allowing for two electric vehicles to charge simultaneously. Each connector can be of a different type, providing flexibility for various vehicle models and charging standards. The DualChargingPoint is designed to optimize the use of charging infrastructure by enabling multiple vehicles to charge at the same time, which can be particularly beneficial in high-traffic areas or for stations with limited space.
/// </summary>
public class DualChargingPoint(List<Connector> leftSide, List<Connector> rightSide) : IChargingPoint
{
    private readonly List<Connector> _leftSide = leftSide;
    private readonly List<Connector> _rightSide = rightSide;

    /// <inheritdoc/>
    public List<Socket> GetSockets()
    {
        var sockets = new List<Socket>();
        sockets.AddRange(_leftSide.Select(c => c.GetSocket()));
        sockets.AddRange(_rightSide.Select(c => c.GetSocket()));
        return sockets.Distinct().ToList();
    }

    /// <summary>
    /// Allocates available power across both connected batteries and computes the expected charging times.
    /// </summary>
    /// <param name="chargingModel"> The charging model to use for computing charging times. </param>
    /// <param name="availablePower"> The total power available for allocation. </param>
    /// <param name="socTarget1"> The target state of charge for the first battery. </param>
    /// <param name="socTarget2"> The target state of charge for the second battery. </param>
    /// <param name="battery1"> The snapshot of the first battery. </param>
    /// <param name="battery2"> The snapshot of the second battery. </param>
    /// <returns> The result of the allocation and computation. </returns>
    public ChargingEstimate AllocateAndCompute(
        ChargingModel chargingModel,
        double availablePower,
        double socTarget1,
        double socTarget2,
        GetBattery battery1,
        GetBattery battery2)
    {
        var allocation = PowerDistributor.DistributeDual(
            availablePower, battery1.MaxChargeRate, battery2.MaxChargeRate);

        var time1 = chargingModel.GetChargingTimeHours(
            battery1.CurrentCharge, socTarget1, battery1.Capacity, allocation.Allocated1);
        var time2 = chargingModel.GetChargingTimeHours(
            battery2.CurrentCharge, socTarget2, battery2.Capacity, allocation.Allocated2);

        return new ChargingEstimate(time1, time2);
    }

    /// <summary>
    /// Allocates available power to a single connected battery and computes the expected charging time
    /// if only one vehicle is connected.
    /// </summary>
    /// <param name="chargingModel"> The charging model to use for computing charging times. </param>
    /// <param name="availablePower"> The total power available for allocation. </param>
    /// <param name="socTarget"> The target state of charge for the battery. </param>
    /// <param name="battery"> The snapshot of the battery. </param>
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
