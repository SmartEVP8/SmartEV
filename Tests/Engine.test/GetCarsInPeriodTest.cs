namespace Engine.test;

using Core.Shared;
using Engine.Vehicles;

/// <summary>
/// This class tests the CarsInPeriod class and methods.
/// </summary>
public class GetCarsInPeriodTest
{
    private readonly Time _spawnFrequency = 15;

    [Fact]
    public void ZeroFraction_ReturnsZero()
    {
        var sut = new CarsInPeriod(_spawnFrequency, 0.0);
        var result = sut.GetCarsInPeriod(1);
        Assert.Equal(0, result);
    }

    [Fact]
    public void DoubleFraction_DoublesResult()
    {
        var time = new Time(1);
        var half = new CarsInPeriod(_spawnFrequency, 0.5);
        var full = new CarsInPeriod(_spawnFrequency, 1.0);
        var halfAmount = half.GetCarsInPeriod(time);
        var fullAmount = full.GetCarsInPeriod(time);

        // Putting ±1 here accounts for truncating.
        Assert.InRange(fullAmount, (halfAmount * 2) - 1, (halfAmount * 2) + 1);
    }
}
