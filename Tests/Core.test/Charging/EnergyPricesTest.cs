using Core.Charging;
using Core.DayCycles;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

/// <summary>
/// Tests for <see cref="EnergyPrices"/>.
/// </summary>
public class EnergyPricesTest
{
    /// <summary>
    /// Verifies that <see cref="EnergyPrices.GetHourPrice"/> returns the correct price for a given hour.
    /// </summary>
    /// <param name="day">The day from DaysOfWeek enum.</param>
    /// <param name="hour">The hour of the day (0–23).</param>
    /// <param name="expected">The expected price in DKK/kWh.</param>
    [Theory]
    [InlineData(DaysOfWeek.Monday, 0, 3.7402f)]
    [InlineData(DaysOfWeek.Wednesday, 15, 2.6900f)]
    [InlineData(DaysOfWeek.Saturday, 23, 3.6926f)]
    public void GetHourPrice_ReturnsExpectedPrice(DaysOfWeek day, int hour, float expected)
    {
        float result = EnergyPrices.GetHourPrice(day, hour);

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Verifies that <see cref="EnergyPrices.GetHourPrice"/> correctly handles values outside the range of 0-23.
    /// </summary>
    /// <param name="day">The day being queried.</param>
    /// <param name="hour">The hour of the day (0–23).</param>
    [Theory]
    [InlineData(DaysOfWeek.Monday, -1)]
    [InlineData(DaysOfWeek.Monday, 24)]
    [InlineData(DaysOfWeek.Monday, 100)]
    public void GetHourPrice_InvalidHour_ThrowsArgumentOutOfRangeException(DaysOfWeek day, int hour) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyPrices.GetHourPrice(day, hour));

    /// <summary>
    /// Verifies that <see cref="EnergyPrices.GetHourPrice"/> correctly handles values outside the range of the DaysOfWeek enum.
    /// </summary>
    [Fact]
    public void GetHourPrice_InvalidDay_ThrowsArgumentOutOfRangeException()
    {
        var validHour = 0;
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyPrices.GetHourPrice((DaysOfWeek)99, validHour));
    }

    /// <summary>
    /// Verifies that <see cref="EnergyPrices.GetDayPrice"/> returns 24 entries for a valid day.
    /// </summary>
    [Fact]
    public void GetDayPrice_ValidDay_Returns24Entries()
    {
        var result = EnergyPrices.GetDayPrice(DaysOfWeek.Monday);
        Assert.Equal(24, result.Count);
    }

    /// <summary>
    /// Verifies that <see cref="EnergyPrices.GetDayPrice"/> returns all hours 0-23 as keys.
    /// </summary>
    [Fact]
    public void GetDayPrice_ValidDay_ContainsAllHours()
    {
        var result = EnergyPrices.GetDayPrice(DaysOfWeek.Monday);
        for (var hour = 0; hour <= 23; hour++)
            Assert.True(result.ContainsKey(hour), $"Missing hour {hour}");
    }

    /// <summary>
    /// Verifies that <see cref="EnergyPrices.GetDayPrice"/> returns the correct price for a known entry.
    /// </summary>
    [Fact]
    public void GetDayPrice_ValidDay_ReturnsCorrectPrice()
    {
        var result = EnergyPrices.GetDayPrice(DaysOfWeek.Monday);
        Assert.Equal(0.554331f, result[0]);
    }

    /// <summary>
    /// Verifies that <see cref="EnergyPrices.GetDayPrice"/> throws for an invalid day.
    /// </summary>
    [Fact]
    public void GetDayPrice_InvalidDay_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyPrices.GetDayPrice((DaysOfWeek)99));
}