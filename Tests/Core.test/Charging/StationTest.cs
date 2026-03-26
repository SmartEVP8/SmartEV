namespace Core.test.Charging;

using Core.Charging;
using Core.Shared;

/// <summary>
/// Tests for <see cref="Station"/>.
/// </summary>
public class StationTest
{
    private readonly EnergyPrices _energyPrices = new(new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "energy_prices.csv")), new Random(42));

    /// <summary>
    /// Verifies that <see cref="Station.UpdatePrice"/> sets <see cref="Station.Price"/>
    /// within ±20% of the base price returned by <see cref="EnergyPrices.GetPrice"/>.
    /// </summary>
    /// <param name="day">The day of the week to check.</param>
    /// <param name="hour">The hour of the day (0–23) to pass to <see cref="Station.UpdatePrice"/>.</param>
    [Theory]
    [InlineData(DayOfWeek.Monday, 0)]
    [InlineData(DayOfWeek.Monday, 12)]
    [InlineData(DayOfWeek.Monday, 18)]
    [InlineData(DayOfWeek.Monday, 23)]
    public void CalculatePrice_SetsPrice_WithinExpectedRange(DayOfWeek day, int hour)
    {
        var station = CreateStation();
        var basePrice = _energyPrices.GetHourPrice(day, hour);

        station.UpdatePrice(DayOfWeek.Monday, hour);

        Assert.InRange(station.Price, basePrice * 0.80f, basePrice * 1.20f);
    }

    /// <summary>
    /// Verifies that <see cref="Station.UpdatePrice"/> changes <see cref="Station.Price"/>
    /// from its initial value.
    /// </summary>
    [Fact]
    public void CalculatePrice_ChangesPrice()
    {
        var station = CreateStation();
        station.UpdatePrice(DayOfWeek.Monday, 12);
        Assert.NotEqual(3.0f, station.Price);
    }

    private Station CreateStation() =>
        new(
            id: 1,
            name: "Test Station",
            address: "Test Street 1",
            position: new Position(10.0, 56.0),
            chargers: [],
            _energyPrices);
}
