using Core.Charging;
using Core.Shared;
using Core.Vehicles;
using Engine.Charging;

/// <summary>
/// Tests for <see cref="ChargingAllocator"/>.
/// </summary>
public class ChargingAllocatorTest
{
    /// <summary>
    /// Verifies that <see cref="ChargingAllocator.AllocateAndCompute"/> returns a positive time
    /// for the first car and zero for the second when using a single charging point.
    /// </summary>
    [Fact]
    public void SinglePoint_ReturnsTimeForOnly1Car()
    {
        var result = _allocator.AllocateAndCompute(_single, 150.0, 0.8, 0.0, MakeBattery(0.2f));

        Assert.True(result.TimeHours1 > 0);
        Assert.Equal(0.0, result.TimeHours2);
    }

    /// <summary>
    /// Verifies that <see cref="ChargingAllocator.AllocateAndCompute"/> returns positive times
    /// for both cars when using a dual charging point.
    /// </summary>
    [Fact]
    public void DualPoint_ReturnsTimeForBothCars()
    {
        var result = _allocator.AllocateAndCompute(_dual, 150.0, 0.8, 0.9, MakeBattery(0.2f), MakeBattery(0.5f));

        Assert.True(result.TimeHours1 > 0);
        Assert.True(result.TimeHours2 > 0);
    }

    /// <summary>
    /// Verifies that <see cref="ChargingAllocator.AllocateAndCompute"/> returns zero charging time
    /// for a car that is already at its target SOC.
    /// </summary>
    [Fact]
    public void CarAlreadyAtTarget_ReturnsZeroTime()
    {
        var result = _allocator.AllocateAndCompute(_single, 150.0, 0.8, 0.0, MakeBattery(0.8f));

        Assert.Equal(0.0, result.TimeHours1);
    }

    /// <summary>
    /// Verifies that <see cref="ChargingAllocator.AllocateAndCompute"/> returns a shorter charging
    /// time when more power is available.
    /// </summary>
    [Fact]
    public void MoreAvailablePower_ResultsInShorterTime()
    {
        var lowPower = _allocator.AllocateAndCompute(_single, 50.0, 0.8, 0.0, MakeBattery(0.2f));
        var highPower = _allocator.AllocateAndCompute(_single, 150.0, 0.8, 0.0, MakeBattery(0.2f));

        Assert.True(highPower.TimeHours1 < lowPower.TimeHours1);
    }

    /// <summary>
    /// Verifies that when one car on a dual charging point has a limited max charge rate,
    /// the other car receives the remaining power and therefore charges faster than in an even split.
    /// </summary>
    [Fact]
    public void DualPoint_LimitedCar_OtherCarChargesFaster()
    {
        var limitedBattery = new Battery(77, 10, 0.2f, Socket.Type2);

        var limitedSplit = _allocator.AllocateAndCompute(_dual, 150.0, 0.8, 0.8, MakeBattery(0.2f), limitedBattery);
        var evenSplit = _allocator.AllocateAndCompute(_dual, 150.0, 0.8, 0.8, MakeBattery(0.2f), MakeBattery(0.2f));

        Assert.True(limitedSplit.TimeHours1 < evenSplit.TimeHours1);
    }

    private readonly ChargingAllocator _allocator = new(new ChargingModel());
    private readonly SingleChargingPoint _single = new();
    private readonly DualChargingPoint _dual = new([], []);

    private static Battery MakeBattery(float soc) =>
        new(77, 150, soc, Socket.Type2);
}