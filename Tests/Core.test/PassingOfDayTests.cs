namespace DayCyclesTests;

using static Core.DayCycles.CarsOnRoad;
using static Core.DayCycles.Days;
using System;
using Xunit;

/// <summary>
/// Tests for the CarsOnRoad class, which estimates the number of EVs
/// on the road based on congestion data.
/// </summary>
public class CarsOnRoadTests
{
    /// <summary>
    /// Tests that providing valid hours (0-23) returns a number of EVs on the road
    /// that is within the expected range (0 to TotalEVs).
    /// </summary>
    [Fact]
    public void ValidHours()
    {
        var day = Day.Monday;

        for (int hour = 0; hour < 24; hour++)
        {
            var evsOnRoad = GetEVsOnRoad(day, hour);
            Assert.InRange(evsOnRoad, 0, TotalEVs);
        }
    }

    /// <summary>
    /// Tests that providing an invalid hour (less than 0 or greater than 23) throws an
    /// ArgumentOutOfRangeException.
    /// </summary>
    /// <param name="hour">The invalid hour to test.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    public void InvalidHour(int hour)
    {
        var day = Day.Monday;

        Assert.Throws<ArgumentOutOfRangeException>(() => GetEVsOnRoad(day, hour));
    }

    /// <summary>
    /// Tests that providing valid day values (0-6 corresponding to Monday-Sunday)
    /// returns a number of EVs on the road that is within the expected range (0 to TotalEVs).
    /// </summary>
    [Fact]
    public void ValidDays()
    {
        for (int dayValue = 0; dayValue < 7; dayValue++)
        {
            var day = (Day)dayValue;
            var evsOnRoad = GetEVsOnRoad(day, 12);
            Assert.InRange(evsOnRoad, 0, TotalEVs);
        }
    }

    /// <summary>
    /// Tests that providing an invalid day value (less than 0 or greater than 6) throws an
    /// ArgumentOutOfRangeException.
    /// </summary>
    /// <param name="invalidDayValue">The invalid day value to test.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    [InlineData(99)]
    public void InvalidDay(int invalidDayValue)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GetEVsOnRoad((Day)invalidDayValue, 12));
    }

    /// <summary>
    /// Tests that the number of EVs on the road for a day with peak congestion does not exceed the
    /// total number of registered EVs in Denmark.
    /// </summary>
    [Fact]
    public void Result_ShouldNeverExceedTotalEVs()
    {
        var day = Day.Tuesday;
        int hour = 7;

        var evsOnRoad = GetEVsOnRoad(day, hour);

        Assert.InRange(evsOnRoad, 0, TotalEVs);
    }
}