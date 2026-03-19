namespace Engine.test;

using static Engine.DayCycles.CarsOnRoad;
using System;
using Xunit;

/// <summary>
/// Tests for the CarsOnRoad class, which estimates the number of EVs
/// on the road based on congestion data.
/// </summary>
public class CarsOnRoadTests
{

    /// <summary>
    /// Tests known day/hour combinations against expected EV counts.
    ///
    /// Expected values are derived from:
    ///   BaselineCars + (PeakCars - BaselineCars) * (congestion / 100)
    ///   = 16,680 + (417,000 - 16,680) * (congestion / 100).
    /// </summary>
    /// <param name="day">The day of the week to test.</param>
    /// <param name="hour">The hour of the day to test (0-23).</param>
    /// <param name="expected">The expected number of EVs on the road.</param>
    [Theory]
    [InlineData(DayOfWeek.Tuesday, 7, BaselineCars + ((PeakCars - BaselineCars) * 100 / 100))]
    [InlineData(DayOfWeek.Sunday, 0, BaselineCars + ((PeakCars - BaselineCars) * 3 / 100))]
    [InlineData(DayOfWeek.Monday, 6, BaselineCars + ((PeakCars - BaselineCars) * 88 / 100))]
    [InlineData(DayOfWeek.Friday, 13, BaselineCars + ((PeakCars - BaselineCars) * 60 / 100))]
    [InlineData(DayOfWeek.Saturday, 12, BaselineCars + ((PeakCars - BaselineCars) * 32 / 100))]
    [InlineData(DayOfWeek.Wednesday, 2, BaselineCars + ((PeakCars - BaselineCars) * 3 / 100))]
    public void ReturnsExpectedEVCount(DayOfWeek day, int hour, double expected)
    {
        var result = GetEVsOnRoad(day, hour);
        Assert.Equal(expected, result, 0);
    }

    /// <summary>
    /// Tests that weekday peak hours produce higher EV counts than the same
    /// hour on a weekend, validating the congestion table is correctly ordered.
    /// </summary>
    [Fact]
    public void WeekdayPeakGTWeekendPeak()
    {
        var mondayPeak = GetEVsOnRoad(DayOfWeek.Monday, 7);
        var sundayPeak = GetEVsOnRoad(DayOfWeek.Sunday, 7);

        Assert.True(
            mondayPeak > sundayPeak,
            $"Expected Monday peak ({mondayPeak}) > Sunday peak ({sundayPeak}).");
    }

    /// <summary>
    /// Tests that Monday rush hour (hour 7) produces more EVs than a quiet
    /// early-morning slot (hour 4), validating intra-day variation.
    /// </summary>
    [Fact]
    public void RushHourGTMorning()
    {
        var rushHour = GetEVsOnRoad(DayOfWeek.Monday, 7);
        var earlyMorning = GetEVsOnRoad(DayOfWeek.Monday, 4);

        Assert.True(
            rushHour > earlyMorning,
            $"Expected rush hour ({rushHour}) traffic to be higher than early morning ({earlyMorning}) traffic.");
    }
}
