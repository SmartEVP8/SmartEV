namespace Core.test.Charging;

using Core.Charging;

/// <summary>
/// Tests for <see cref="EnergyPrices"/>.
/// </summary>
public class EnergyPricesTest
{
    private readonly EnergyPrices _energyPrices = new(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "energy_prices.csv")), new Random(42));

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
        var result = _energyPrices.GetHourPrice(day, hour);

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
        Assert.Throws<ArgumentOutOfRangeException>(() => _energyPrices.GetHourPrice(day, hour));

    /// <summary>
    /// Verifies that <see cref="EnergyPrices.GetHourPrice"/> correctly handles values outside the range of the DayOfWeek enum.
    /// </summary>
    [Fact]
    public void GetHourPrice_InvalidDay_ThrowsArgumentOutOfRangeException() => _ = Assert.Throws<ArgumentOutOfRangeException>(() => _energyPrices.GetHourPrice((DayOfWeek)99, 0));

    /// <summary>
    /// Verifies that <see cref="EnergyPrices.CalculatePrice"/> returns a price within ±20% of the base price.
    /// </summary>
    /// <param name="day">The day of the week.</param>
    /// <param name="hour">The hour of the day (0–23).</param>
    [Theory]
    [InlineData(DayOfWeek.Monday, 0)]
    [InlineData(DayOfWeek.Wednesday, 15)]
    [InlineData(DayOfWeek.Saturday, 23)]
    public void CalculatePrice_ReturnsVarianceWithinRange(DayOfWeek day, int hour)
    {
        var basePrice = _energyPrices.GetHourPrice(day, hour);
        var calculatedPrice = _energyPrices.CalculatePrice(day, hour);

        Assert.InRange(calculatedPrice, basePrice * 0.80f, basePrice * 1.20f);
    }
}
