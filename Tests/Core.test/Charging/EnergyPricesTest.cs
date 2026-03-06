using Core.Charging;

/// <summary>
/// Tests for <see cref="EnergyPrices"/>.
/// </summary>
public class EnergyPricesTest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EnergyPricesTest"/> class.
    /// </summary>
    public EnergyPricesTest()
    {
        var csvPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "energy_prices.csv");
        EnergyPrices.Initialize(csvPath);
    }

    /// <summary>
    /// Verifies that <see cref="EnergyPrices.GetHourPrice"/> returns the correct price for a given hour.
    /// </summary>
    /// <param name="day">The day from DayOfWeek enum.</param>
    /// <param name="hour">The hour of the day (0–23).</param>
    /// <param name="expected">The expected price in DKK/kWh.</param>
    [Theory]
    [InlineData(DayOfWeek.Monday, 0, 2.745128f)]
    [InlineData(DayOfWeek.Wednesday, 15, 3.710836f)]
    [InlineData(DayOfWeek.Saturday, 23, 3.009931f)]
    public void GetHourPrice_ReturnsExpectedPrice(DayOfWeek day, int hour, float expected)
    {
        var result = EnergyPrices.GetHourPrice(day, hour);

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Verifies that <see cref="EnergyPrices.GetHourPrice"/> correctly handles values outside the range of 0-23.
    /// </summary>
    /// <param name="day">The day being queried.</param>
    /// <param name="hour">The hour of the day (0–23).</param>
    [Theory]
    [InlineData(DayOfWeek.Monday, -1)]
    [InlineData(DayOfWeek.Monday, 24)]
    [InlineData(DayOfWeek.Monday, 100)]
    public void GetHourPrice_InvalidHour_ThrowsArgumentOutOfRangeException(DayOfWeek day, int hour) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyPrices.GetHourPrice(day, hour));

    /// <summary>
    /// Verifies that <see cref="EnergyPrices.GetHourPrice"/> correctly handles values outside the range of the DayOfWeek enum.
    /// </summary>
    [Fact]
    public void GetHourPrice_InvalidDay_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyPrices.GetHourPrice((DayOfWeek)99, 0));
    }

    /// <summary>
    /// Verifies that <see cref="EnergyPrices.GetDayPrice"/> returns exactly 24 entries containing all hours 0-23.
    /// </summary>
    [Fact]
    public void GetDayPrice_ValidDay_Returns24EntriesWithAllHours()
    {
        var result = EnergyPrices.GetDayPrice(DayOfWeek.Monday);
        Assert.Equal(24, result.Count);
        for (var hour = 0; hour <= 23; hour++)
            Assert.True(result.ContainsKey(hour), $"Missing hour {hour}");
    }

    /// <summary>
    /// Verifies that <see cref="EnergyPrices.GetDayPrice"/> returns the correct price for a known entry.
    /// </summary>
    [Fact]
    public void GetDayPrice_ValidDay_ReturnsCorrectPrice()
    {
        var result = EnergyPrices.GetDayPrice(DayOfWeek.Monday);
        Assert.Equal(2.745128f, result[0]);
    }

    /// <summary>
    /// Verifies that <see cref="EnergyPrices.GetDayPrice"/> throws for an invalid day.
    /// </summary>
    [Fact]
    public void GetDayPrice_InvalidDay_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyPrices.GetDayPrice((DayOfWeek)99));
}