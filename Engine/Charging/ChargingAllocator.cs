namespace Engine.Charging;

using Core.Charging;
using Core.Vehicles;

/// <summary>
/// ChargingAllocator provides a unified interface for allocating available power to electric vehicles
/// based on the type of charging point and the capacities of the connected batteries.
/// </summary>
public static class ChargingAllocator
{
    /// <summary>
    /// Allocates available power to one or two batteries based on the type of charging point.
    /// </summary>
    /// <param name="chargingPoint">The charging point for which to allocate power.</param>
    /// <param name="availablePower">The total power available for allocation.</param>
    /// <param name="batteries">The batteries to which power should be allocated.</param>
    /// <returns>The result of the power allocation.</returns>
    public static AllocationResult Allocate(IChargingPoint chargingPoint, double availablePower, params Battery[] batteries)
    {
        return chargingPoint switch
        {
            SingleChargingPoint => AllocateSingle(availablePower, batteries[0]),
            DualChargingPoint => AllocateDual(availablePower, batteries[0], batteries[1]),
            _ => throw new ArgumentException("Unknown charging point type", nameof(chargingPoint))
        };
    }

    /// <summary>
    /// Allocates power for a single charging point, considering only one battery's capacity.
    /// </summary>
    /// <param name="availablePower">The total power available for allocation.</param>
    /// <param name="battery">The battery to which power should be allocated.</param>
    /// <returns>The result of the power allocation.</returns>
    private static AllocationResult AllocateSingle(double availablePower, Battery battery)
    {
        var allocated = Math.Min(availablePower, battery.MaxChargeRate);
        return new AllocationResult(allocated, 0, availablePower - allocated);
    }

    /// <summary>
    /// Allocates power for a dual charging point, distributing available power between
    /// two batteries using a fair-split algorithm.
    /// </summary>
    /// <param name="availablePower">The total power available for allocation.</param>
    /// <param name="battery1">The first battery to which power should be allocated.</param>
    /// <param name="battery2">The second battery to which power should be allocated.</param>
    /// <returns>The result of the power allocation.</returns>
    private static AllocationResult AllocateDual(double availablePower, Battery battery1, Battery battery2)
    {
        var (a1, a2, wasted) = PowerDistributor.Distribute(availablePower, battery1.MaxChargeRate, battery2.MaxChargeRate);
        return new AllocationResult(a1, a2, wasted);
    }
}