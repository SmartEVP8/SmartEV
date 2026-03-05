namespace DayCyclesTests;

/// <summary>
/// Tests for the PassingOfDay class, which estimates the number of EVs
/// on the road based on congestion data.
/// </summary>
public class PassingOfDayTests
{

    /// <summary>
    /// Tests that providing valid hours (0-23) returns a number of EVs on the road
    /// that is within the expected range (0 to TotalEVs).
    /// </summary>
    [Fact]
    public void ValidHours()
    {
        var day = Core.DayCycles.PassingOfDay.Day.Monday;

        for (int hour = 0; hour < 24; hour++)
        {
            int evsOnRoad = Core.DayCycles.PassingOfDay.GetEVsOnRoad(day, hour);
            Assert.InRange(evsOnRoad, 0, Core.DayCycles.PassingOfDay.TotalEVs);
        }
    }

    /// <summary>
    /// Tests that providing an invalid hour (less than 0 or greater than 23) throws an ArgumentOutOfRangeException.
    /// This ensures that the method correctly validates the hour input and does not allow out-of-range values.
    /// The test iterates over a set of invalid hours and asserts that the expected exception is thrown for each case.
    /// </summary>
    /// <param name="hour">The invalid hour to test.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    public void InvalidHour(int hour)
    {
        var day = Core.DayCycles.PassingOfDay.Day.Monday;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Core.DayCycles.PassingOfDay.GetEVsOnRoad(day, hour));
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
            var day = (Core.DayCycles.PassingOfDay.Day)dayValue;
            int evsOnRoad = Core.DayCycles.PassingOfDay.GetEVsOnRoad(day, 12);
            Assert.InRange(evsOnRoad, 0, Core.DayCycles.PassingOfDay.TotalEVs);
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
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Core.DayCycles.PassingOfDay.GetEVsOnRoad((Core.DayCycles.PassingOfDay.Day)invalidDayValue, 12));
    }

    /// <summary>
    /// Tests that the number of EVs on the road for a day with peak congestion does not exceed the
    /// total number of registered EVs in Denmark.
    /// </summary>
    [Fact]
    public void Result_ShouldNeverExceedTotalEVs()
    {
        var day = Core.DayCycles.PassingOfDay.Day.Tuesday;
        int hour = 7;

        int evsOnRoad = Core.DayCycles.PassingOfDay.GetEVsOnRoad(day, hour);

        Assert.InRange(evsOnRoad, 0, Core.DayCycles.PassingOfDay.TotalEVs);
    }
}