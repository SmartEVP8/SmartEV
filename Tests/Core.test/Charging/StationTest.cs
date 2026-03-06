using Core.Charging;
using Core.DayCycles;
using Core.Shared;

/// <summary>
/// Tests for <see cref="Station"/>.
/// </summary>
public class StationTest
{
    private static Station CreateStation(float price = 3.0f, Random? random = null) =>
        new (id: 1, name: "Test Station", address: "Test Street 1",
            position: new Position(10.0, 56.0), chargers: null, price: price,
            random: random ?? new Random(42));

    /// <summary>
    /// Verifies that the constructor sets <see cref="Station.Price"/> to the supplied value.
    /// </summary>
    /// <param name="price">The initial price to supply.</param>
    [Theory]
    [InlineData(2.69f)]
    [InlineData(5.14f)]
    [InlineData(0.0f)]
    public void Constructor_SetsPrice(float price)
    {
        Station station = CreateStation(price);

        Assert.Equal(price, station.Price);
    }

    /// <summary>
    /// Verifies that <see cref="Station.CalculatePrice"/> sets <see cref="Station.Price"/>
    /// within ±20% of the base price returned by <see cref="EnergyPrices.GetPrice"/>.
    /// </summary>
    /// <param name="day">The day of the week to check.</param>
    /// <param name="hour">The hour of the day (0–23) to pass to <see cref="Station.CalculatePrice"/>.</param>
    [Theory]
    [InlineData(DaysOfWeek.Monday, 0)]
    [InlineData(DaysOfWeek.Monday, 12)]
    [InlineData(DaysOfWeek.Monday, 18)]
    [InlineData(DaysOfWeek.Monday, 23)]
    public void CalculatePrice_SetsPrice_WithinExpectedRange(DaysOfWeek day, int hour)
    {
        Station station = CreateStation();
        float basePrice = EnergyPrices.GetHourPrice(day, hour);

        station.CalculatePrice(hour);

        Assert.InRange(station.Price, basePrice * 0.80f, basePrice * 1.20f);
    }

    /// <summary>
    /// Verifies that <see cref="Station.CalculatePrice"/> changes <see cref="Station.Price"/>
    /// from its initial value.
    /// </summary>
    [Fact]
    public void CalculatePrice_ChangesPrice()
    {
        Station station = CreateStation(random: new Random(42));
        station.CalculatePrice(12);
        Assert.NotEqual(3.0f, station.Price);
    }
}
