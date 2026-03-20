using System;
using Engine.Vehicles;

/// <summary>
/// This class tests the CarsInPeriod class and methods.
/// </summary>
public class GetCarsInPeriodTest
{
    private const uint _testPeriod = 900; // 900 seconds = 15 minutes
    private readonly CarsInPeriod _sut = new();

    [Fact]
    public void ZeroFraction_ReturnsZero()
    {
        var result = _sut.GetCarsInPeriod(DayOfWeek.Monday, 8, 0.0, _testPeriod);

        Assert.Equal(0, result);
    }

    [Fact]
    public void DoubleFraction_DoublesResult()
    {
        var half = _sut.GetCarsInPeriod(DayOfWeek.Monday, 8, 0.5, _testPeriod);
        var full = _sut.GetCarsInPeriod(DayOfWeek.Monday, 8, 1.0, _testPeriod);

        // Putting ±1 here accounts for truncating.
        Assert.InRange(full, (half * 2) - 1, (half * 2) + 1);
    }

    [Fact]
    public void PeakHour_ReturnsMoreThanOffPeakHour()
    {
        var peak = _sut.GetCarsInPeriod(DayOfWeek.Monday, 8, 0.5, _testPeriod);
        var offPeak = _sut.GetCarsInPeriod(DayOfWeek.Sunday, 2, 0.5, _testPeriod);

        Assert.True(peak > offPeak);
    }

    [Fact]
    public void GetCarsInPeriod_NeverReturnsNegative()
    {
        var result = _sut.GetCarsInPeriod(DayOfWeek.Sunday, 2, 0.5, _testPeriod);

        Assert.True(result >= 0);
    }
}