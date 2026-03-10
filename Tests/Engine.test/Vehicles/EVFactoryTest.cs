using Core.Shared;
using Core.Vehicles.Configs;
using Engine.Vehicles;

/// <summary>
/// Tests for the EVFactory class.
/// </summary>
public class EVFactoryTest
{
    /// <summary>
    /// Initialization of an EVFactory.
    /// </summary>
    /// <param name="seed">The seed used to create EVs.</param>
    /// <returns>An EVFactory.</returns>
    private static EVFactory MakeFactory(int seed = 42) => new (
        new EVConfig(
            [
                new WeightedBatteryConfig(new BatteryConfig(7, 40, 77, Socket.CCS), weight: 1.0),
                new WeightedBatteryConfig(new BatteryConfig(3, 20, 50, Socket.Type2), weight: 1.0),
            ],
            new PrefsConfig(0f, 1f)),
        new Random(seed));

    /// <summary>
    /// Verifies that the create function correctly assigns incrementing ids.
    /// </summary>
    [Fact]
    public void Create_AssignsIncrementingIds()
    {
        var factory = MakeFactory();
        var first = factory.Create();
        var second = factory.Create();
        Assert.Equal(1u, first.Id);
        Assert.Equal(2u, second.Id);
    }

    /// <summary>
    /// Verifies that the price sensitivity stays within the configured min/max range.
    /// </summary>
    /// <param name="min">The configured minimum price sensitivity.</param>
    /// <param name="max">The configured maximum price sensitivity.</param>
    [Theory]
    [InlineData(0.5f, 0.8f)]
    [InlineData(0.1f, 0.3f)]
    public void Create_PriceSensitivityWithinConfiguredRange(float min, float max)
    {
        var factory = new EVFactory(
            new EVConfig(
                [new WeightedBatteryConfig(new BatteryConfig(7, 40, 77, Socket.CCS), weight: 1.0)],
                new PrefsConfig(min, max)),
            new Random(42));

        for (var i = 0; i < 20; i++)
        {
            var ev = factory.Create();
            Assert.InRange(ev.Preferences.PriceSensitivity, min, max);
        }
    }

    /// <summary>
    /// Verifies that the CreateFleet function correctly returns the specified amount of EVs.
    /// </summary>
    /// <param name="amount">The amount of EVs to generate.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void CreateFleet_ReturnsCorrectAmount(int amount)
    {
        var fleet = MakeFactory().CreateFleet(amount);
        Assert.Equal(amount, fleet.Count);
    }

    /// <summary>
    /// Verifies that EVs generated from CreateFleet correctly have incrementing IDs.
    /// </summary>
    [Fact]
    public void CreateFleet_AllEVsHaveUniqueIds()
    {
        var fleet = MakeFactory().CreateFleet(50);
        var ids = fleet.Select(ev => ev.Id).ToHashSet();
        Assert.Equal(fleet.Count, ids.Count);
    }
}