using Engine.Charging;

/// <summary>
/// Tests for <see cref="PowerDistributor"/>.
/// </summary>
public class PowerDistributorTest
{
    /// <summary>
    /// Verifies that <see cref="PowerDistributor.DistributeSingle"/> allocates all available power
    /// when it is within the consumer's capacity.
    /// </summary>
    [Fact]
    public void DistributeSingle_UnderCapacity_AllocatesAllPower()
    {
        var result = PowerDistributor.DistributeSingle(100.0, 150.0);

        Assert.Equal(100.0, result.Allocated1);
        Assert.Equal(0.0, result.Wasted);
    }

    /// <summary>
    /// Verifies that <see cref="PowerDistributor.DistributeSingle"/> caps allocation at capacity
    /// and marks the remainder as wasted.
    /// </summary>
    [Fact]
    public void DistributeSingle_OverCapacity_CapsAtCapacityAndWastes()
    {
        var result = PowerDistributor.DistributeSingle(150.0, 100.0);

        Assert.Equal(100.0, result.Allocated1);
        Assert.Equal(50.0, result.Wasted);
    }

    /// <summary>
    /// Verifies that <see cref="PowerDistributor.DistributeSingle"/> always sets Allocated2 to zero.
    /// </summary>
    [Fact]
    public void DistributeSingle_Always_ReturnsZeroForAllocated2()
    {
        var result = PowerDistributor.DistributeSingle(100.0, 150.0);

        Assert.Equal(0.0, result.Allocated2);
    }

    /// <summary>
    /// Verifies that <see cref="PowerDistributor.DistributeDual"/> splits power evenly
    /// between two consumers with equal capacities.
    /// </summary>
    [Fact]
    public void DistributeDual_EqualCapacities_SplitsEvenly()
    {
        var result = PowerDistributor.DistributeDual(100.0, 50.0, 50.0);

        Assert.Equal(50.0, result.Allocated1);
        Assert.Equal(50.0, result.Allocated2);
        Assert.Equal(0.0, result.Wasted);
    }

    /// <summary>
    /// Verifies that <see cref="PowerDistributor.DistributeDual"/> offers remaining power
    /// to the other consumer when one is limited by its capacity.
    /// </summary>
    [Fact]
    public void DistributeDual_OneConsumerLimited_RemainingOfferedToOther()
    {
        var result = PowerDistributor.DistributeDual(100.0, 20.0, 100.0);

        Assert.Equal(20.0, result.Allocated1);
        Assert.Equal(80.0, result.Allocated2);
        Assert.Equal(0.0, result.Wasted);
    }

    /// <summary>
    /// Verifies that <see cref="PowerDistributor.DistributeDual"/> marks power as wasted
    /// when both consumers are limited below the total available power.
    /// </summary>
    [Fact]
    public void DistributeDual_BothConsumersLimited_PowerIsWasted()
    {
        var result = PowerDistributor.DistributeDual(150.0, 40.0, 40.0);

        Assert.Equal(40.0, result.Allocated1);
        Assert.Equal(40.0, result.Allocated2);
        Assert.Equal(70.0, result.Wasted);
    }

    /// <summary>
    /// Verifies that <see cref="PowerDistributor.DistributeDual"/> never allocates more
    /// than the total available power across both consumers.
    /// </summary>
    /// <param name="available">Total power available.</param>
    /// <param name="capacity1">Maximum power consumer 1 can take.</param>
    /// <param name="capacity2">Maximum power consumer 2 can take.</param>
    [Theory]
    [InlineData(100.0, 60.0, 60.0)]
    [InlineData(150.0, 100.0, 100.0)]
    [InlineData(50.0, 30.0, 30.0)]
    public void DistributeDual_TotalAllocatedNeverExceedsAvailable(double available, double capacity1, double capacity2)
    {
        var result = PowerDistributor.DistributeDual(available, capacity1, capacity2);

        Assert.True(result.Allocated1 + result.Allocated2 <= available);
    }
}