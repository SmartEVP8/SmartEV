namespace Engine.Charging;

using Core.Charging;
using Core.Vehicles;

/// <summary>
/// ChargingAllocator provides a unified interface for allocating available power to electric vehicles
/// based on the type of charging point and the capacities of the connected batteries.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ChargingAllocator"/> class.
/// </remarks>
/// <param name="chargingModel">The charging model to use for computing charging times.</param>
public class ChargingAllocator(ChargingModel chargingModel)
{
    /// <summary>
    /// Allocates available power to the connected batteries based on the type of charging point
    /// and computes the expected charging times.
    /// </summary>
    /// <param name="chargingPoint">The charging point to allocate power to.</param>
    /// <param name="availablePower">The total power available for allocation.</param>
    /// <param name="socTarget1">The target state of charge for the first battery.</param>
    /// <param name="socTarget2">The target state of charge for the second battery.
    ///                          This is ignored for single-charging points.</param>
    /// <param name="batteries">The batteries to allocate power to.</param>
    /// <returns>The result of the allocation and computation.</returns>
    /// <exception cref="ArgumentException">Thrown when the charging point type is unknown.</exception>
    public ChargingEstimate AllocateAndCompute(
        IChargingPoint chargingPoint,
        double availablePower,
        double socTarget1,
        double socTarget2,
        params Battery[] batteries)
    {
        var allocation = chargingPoint switch
        {
            SingleChargingPoint => PowerDistributor.DistributeSingle(availablePower, batteries[0].MaxChargeRate),
            DualChargingPoint => PowerDistributor.DistributeDual(availablePower, batteries[0].MaxChargeRate, batteries[1].MaxChargeRate),
            _ => throw new ArgumentException("Unknown charging point type", nameof(chargingPoint))
        };

        var time1 = chargingModel.GetChargingTimeHours(
            batteries[0].CurrentCharge, socTarget1, batteries[0].Capacity, allocation.Allocated1);

        var time2 = batteries.Length > 1
            ? chargingModel.GetChargingTimeHours(
                batteries[1].CurrentCharge, socTarget2, batteries[1].Capacity, allocation.Allocated2)
            : 0.0;

        return new ChargingEstimate(time1, time2);
    }
}