using Core.Charging;

/// <summary>
/// Tests for <see cref="EnergyPrices"/>.
/// </summary>
public class EnergyPricesTest
{
    /// <summary>
    /// Verifies that <see cref="EnergyPrices.GetPrice"/> returns the correct price for a given hour.
    /// </summary>
    /// <param name="hour">The hour of the day (0–23).</param>
    /// <param name="expected">The expected price in DKK/kWh.</param>
    [Theory]
    [InlineData(0, 3.7402f)]
    [InlineData(15, 2.6900f)]
    [InlineData(18, 5.1400f)]
    [InlineData(23, 3.6926f)]
    public void GetPrice_ReturnsExpectedPrice(int hour, float expected)
    {
        float result = EnergyPrices.GetPrice(hour);

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Verifies that <see cref="EnergyPrices.GetPrice"/> correctly handles values outside the range of 0-23.
    /// </summary>
    /// <param name="hour">The hour of the day (0–23).</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(24)]
    [InlineData(100)]
    public void GetPrice_InvalidHour_ThrowsArgumentOutOfRangeException(int hour)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EnergyPrices.GetPrice(hour));
    }
}