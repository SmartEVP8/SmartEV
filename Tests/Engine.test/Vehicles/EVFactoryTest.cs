using Engine.Vehicles;

/// <summary>
/// Tests for the EVFactory class.
/// </summary>
public class EVFactoryTest
{
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
    /// Verifies that the price sensitivity stays within [0, 1).
    /// </summary>
    [Fact]
    public void Create_PriceSensitivityWithinUnitRange()
    {
        var factory = MakeFactory();

        for (var i = 0; i < 20; i++)
        {
            var ev = factory.Create();
            Assert.InRange(ev.Preferences.PriceSensitivity, 0f, 1f);
        }
    }

    /// <summary>
    /// Verifies that PopulateFleet fills an empty buffer.
    /// </summary>
    [Fact]
    public void PopulateFleet_FillsEmptyBuffer()
    {
        var fleet = new Core.Vehicles.EV[10];

        MakeFactory().PopulateFleet(fleet);

        Assert.All(fleet, ev => Assert.NotNull(ev));
        Assert.Equal(1u, fleet[0].Id);
        Assert.Equal(10u, fleet[9].Id);
    }

    /// <summary>
    /// Verifies that PopulateFleet appends after existing EVs in the buffer.
    /// </summary>
    [Fact]
    public void PopulateFleet_AppendsAfterExistingEntries()
    {
        var factory = MakeFactory();
        var fleet = new Core.Vehicles.EV[12];

        for (var i = 0; i < 5; i++)
            fleet[i] = factory.Create();

        factory.PopulateFleet(fleet);

        Assert.Equal(1u, fleet[0].Id);
        Assert.Equal(5u, fleet[4].Id);
        Assert.Equal(6u, fleet[5].Id);
        Assert.Equal(12u, fleet[11].Id);

        var ids = fleet.Select(ev => ev.Id).ToHashSet();
        Assert.Equal(fleet.Length, ids.Count);
    }

    /// <summary>
    /// Verifies that PopulateFleet leaves a fully populated buffer unchanged.
    /// </summary>
    [Fact]
    public void PopulateFleet_DoesNotChangeFullBuffer()
    {
        var factory = MakeFactory();
        var fleet = new Core.Vehicles.EV[6];
        factory.PopulateFleet(fleet);

        var before = fleet.Select(ev => ev.Id).ToArray();

        factory.PopulateFleet(fleet);

        var after = fleet.Select(ev => ev.Id).ToArray();
        Assert.Equal(before, after);
    }

    /// <summary>
    /// Initialization of an EVFactory.
    /// </summary>
    /// <param name="seed">The seed used to create EVs.</param>
    /// <returns>An EVFactory.</returns>
    private static EVFactory MakeFactory(int seed = 42) => new(new Random(seed));
}